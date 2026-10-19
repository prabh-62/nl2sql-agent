using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace Nl2Sql.Data;

/// <summary>The guard's verdict. <see cref="Errors"/> are written for the model, so it can fix its query.</summary>
public sealed record SqlGuardResult(bool IsAllowed, IReadOnlyList<string> Errors, IReadOnlyList<string> Views)
{
    public static SqlGuardResult Allowed(IReadOnlyList<string> views) => new(true, [], views);

    public static SqlGuardResult Rejected(IReadOnlyList<string> errors) => new(false, errors, []);
}

/// <summary>
/// Decides, before anything reaches the database, whether model-generated SQL may run. It parses the
/// text with Microsoft's T-SQL parser and allows exactly one <c>SELECT</c> that reads only views in
/// schema <c>ai</c>. It's an allow-list: table sources, functions and expressions it doesn't know are
/// rejected.
/// <para>
/// The guard is one of three independent layers. Even if it let something through, the database user
/// can only <c>SELECT</c> from schema <c>ai</c>, and row-level security limits the rows it can see.
/// </para>
/// </summary>
public static class SqlGuard
{
    public const int MaxLength = 10_000;

    public const string AllowedSchema = "ai";

    // Built-in functions that reveal or depend on metadata, identity, security or session state.
    // None is needed to answer a business question, and some would let a query probe outside the
    // semantic layer.
    private static readonly HashSet<string> BlockedFunctions = new(StringComparer.OrdinalIgnoreCase)
    {
        "APP_NAME", "CERTENCODED", "CERTPRIVATEKEY", "COL_LENGTH", "COL_NAME", "COLUMNPROPERTY", "CONNECTIONPROPERTY",
        "CONTEXT_INFO", "CURRENT_REQUEST_ID", "CURRENT_TRANSACTION_ID", "DATABASE_PRINCIPAL_ID", "DATABASEPROPERTYEX",
        "DB_ID", "DB_NAME", "DECRYPTBYASYMKEY", "DECRYPTBYCERT", "DECRYPTBYKEY", "DECRYPTBYKEYAUTOASYMKEY",
        "DECRYPTBYKEYAUTOCERT", "DECRYPTBYPASSPHRASE", "ERROR_MESSAGE", "FILE_ID", "FILE_IDEX", "FILE_NAME",
        "FILEGROUP_ID", "FILEGROUP_NAME", "FILEGROUPPROPERTY", "FILEPROPERTY", "FULLTEXTCATALOGPROPERTY",
        "FULLTEXTSERVICEPROPERTY", "GET_FILESTREAM_TRANSACTION_CONTEXT", "HAS_DBACCESS", "HAS_PERMS_BY_NAME",
        "HOST_ID", "HOST_NAME", "IDENT_CURRENT", "IDENT_INCR", "IDENT_SEED", "INDEX_COL", "INDEXKEY_PROPERTY",
        "INDEXPROPERTY", "IS_MEMBER", "IS_ROLEMEMBER", "IS_SRVROLEMEMBER", "KEY_GUID", "KEY_ID", "LOGINPROPERTY",
        "OBJECT_DEFINITION", "OBJECT_ID", "OBJECT_NAME", "OBJECT_SCHEMA_NAME", "OBJECTPROPERTY", "OBJECTPROPERTYEX",
        "ORIGINAL_DB_NAME", "ORIGINAL_LOGIN", "PERMISSIONS", "PWDCOMPARE", "PWDENCRYPT", "SCHEMA_ID", "SCHEMA_NAME",
        "SERVERPROPERTY", "SESSION_CONTEXT", "SESSION_ID", "SESSIONPROPERTY", "SIGNBYASYMKEY", "SIGNBYCERT",
        "SUSER_ID", "SUSER_NAME", "SUSER_SID", "SUSER_SNAME", "TYPE_ID", "TYPE_NAME", "TYPEPROPERTY", "USER_ID",
        "USER_NAME", "VERIFYSIGNEDBYASYMKEY", "VERIFYSIGNEDBYCERT",
    };

    public static SqlGuardResult Check(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
        {
            return SqlGuardResult.Rejected(["The query is empty."]);
        }

        if (sql.Length > MaxLength)
        {
            return SqlGuardResult.Rejected([$"The query is longer than {MaxLength} characters."]);
        }

        var parser = new TSql180Parser(initialQuotedIdentifiers: true);
        TSqlFragment fragment;
        IList<ParseError> parseErrors;
        using (var reader = new StringReader(sql))
        {
            fragment = parser.Parse(reader, out parseErrors);
        }

        if (parseErrors.Count > 0)
        {
            return SqlGuardResult.Rejected(parseErrors
                .Take(3)
                .Select(e => $"Syntax error at line {e.Line}, column {e.Column}: {e.Message}")
                .ToArray());
        }

        if (fragment is not TSqlScript script
            || script.Batches.Count != 1
            || script.Batches[0].Statements.Count != 1)
        {
            return SqlGuardResult.Rejected(["Send exactly one SELECT statement: no batches, no multiple statements."]);
        }

        if (script.Batches[0].Statements[0] is not SelectStatement select)
        {
            return SqlGuardResult.Rejected(["Only SELECT statements are allowed."]);
        }

        if (select.Into is not null)
        {
            return SqlGuardResult.Rejected(["SELECT ... INTO is not allowed: queries can't create tables."]);
        }

        // Collect CTE names first, so unqualified references to them aren't mistaken for tables.
        var cteNames = select.WithCtesAndXmlNamespaces?.CommonTableExpressions.Select(c => c.ExpressionName.Value) ?? [];
        var visitor = new AllowListVisitor(cteNames);
        select.Accept(visitor);

        return visitor.Errors.Count > 0
            ? SqlGuardResult.Rejected(visitor.Errors.Distinct().ToArray())
            : SqlGuardResult.Allowed(visitor.Views.Order(StringComparer.OrdinalIgnoreCase).ToArray());
    }

    private sealed class AllowListVisitor(IEnumerable<string> cteNames) : TSqlFragmentVisitor
    {
        private readonly HashSet<string> _cteNames = new(cteNames, StringComparer.OrdinalIgnoreCase);

        public List<string> Errors { get; } = [];

        public HashSet<string> Views { get; } = new(StringComparer.OrdinalIgnoreCase);

        public override void Visit(TableReference node)
        {
            switch (node)
            {
                case NamedTableReference named:
                    CheckNamedTable(named);
                    break;
                case QueryDerivedTable or JoinTableReference or JoinParenthesisTableReference
                    or InlineDerivedTable or PivotedTableReference or UnpivotedTableReference:
                    break;
                default:
                    Errors.Add($"{Describe(node)} is not allowed. Read only from views in schema '{AllowedSchema}'.");
                    break;
            }
        }

        public override void Visit(FunctionCall node)
        {
            if (node.CallTarget is not null)
            {
                Errors.Add($"Calling '{Script(node.CallTarget)}{node.FunctionName.Value}' is not allowed: only built-in functions can be used.");
            }
            else if (BlockedFunctions.Contains(node.FunctionName.Value))
            {
                Errors.Add($"The function {node.FunctionName.Value.ToUpperInvariant()} is not allowed.");
            }
        }

        public override void Visit(ParameterlessCall node)
        {
            // CURRENT_TIMESTAMP is fine; CURRENT_USER, SESSION_USER, SYSTEM_USER and USER reveal identity.
            if (node.ParameterlessCallType != ParameterlessCallType.CurrentTimestamp)
            {
                Errors.Add($"{node.ParameterlessCallType} is not allowed.");
            }
        }

        public override void Visit(GlobalVariableExpression node) =>
            Errors.Add($"System variables such as {node.Name} are not allowed.");

        public override void Visit(VariableReference node) =>
            Errors.Add($"Variables such as {node.Name} are not allowed.");

        public override void Visit(NextValueForExpression node) =>
            Errors.Add("NEXT VALUE FOR is not allowed: it changes a sequence.");

        public override void Visit(TableHint node) =>
            Errors.Add("Table hints (WITH (...) after a table) are not allowed.");

        public override void Visit(OptimizerHint node) =>
            Errors.Add("Query hints (OPTION (...)) are not allowed.");

        private void CheckNamedTable(NamedTableReference node)
        {
            var name = node.SchemaObject;
            if (name.ServerIdentifier is not null || name.DatabaseIdentifier is not null)
            {
                Errors.Add($"'{Script(name)}' names another database or server. Use two-part names like {AllowedSchema}.Orders.");
                return;
            }

            if (name.SchemaIdentifier is null)
            {
                if (!_cteNames.Contains(name.BaseIdentifier.Value))
                {
                    Errors.Add($"'{name.BaseIdentifier.Value}' needs its schema: write {AllowedSchema}.{name.BaseIdentifier.Value}.");
                }

                return;
            }

            if (!string.Equals(name.SchemaIdentifier.Value, AllowedSchema, StringComparison.OrdinalIgnoreCase))
            {
                Errors.Add($"'{Script(name)}' is outside schema '{AllowedSchema}'. Only the {AllowedSchema} views can be queried.");
                return;
            }

            Views.Add($"{AllowedSchema}.{name.BaseIdentifier.Value}");
        }

        private static string Describe(TableReference node) => node switch
        {
            OpenRowsetTableReference or BulkOpenRowset or InternalOpenRowset => "OPENROWSET",
            OpenQueryTableReference => "OPENQUERY",
            AdHocTableReference => "OPENDATASOURCE",
            OpenXmlTableReference => "OPENXML",
            OpenJsonTableReference => "OPENJSON",
            VariableTableReference => "A table variable",
            _ => $"'{Script(node)}'",
        };

        private static string Script(TSqlFragment fragment) =>
            string.Concat(Enumerable.Range(fragment.FirstTokenIndex, fragment.LastTokenIndex - fragment.FirstTokenIndex + 1)
                .Select(i => fragment.ScriptTokenStream[i].Text));
    }
}

# Architecture

The [README](../README.md) has the overview. This page goes one level deeper. Each diagram links to the decision record that explains it.

---

## 1. Three locks on every query

Model-generated SQL has to get past three independent checks. Each would stop a harmful query on its own. ([ADR 0002](adr/0002-deterministic-sql-guard.md), [ADR 0006](adr/0006-two-level-row-level-security.md))

```mermaid
%%{init: {'theme':'base'}}%%
flowchart TB
    SQL["🤖 SQL written by the model"] --> G

    subgraph L1["🚧 Lock 1 · SQL guard (C#, before the database)"]
        G{"ScriptDom parse:<br/>exactly one SELECT?<br/>only ai.* views?<br/>no INTO, EXEC, OPENROWSET,<br/>variables, hints, metadata functions?"}
    end
    G -- "❌ rejected · reason sent back to the model" --> R["Model fixes its query<br/>(2 retries max)"]
    G -- "✅" --> C

    subgraph L2["🔑 Lock 2 · read-only identity (database permissions)"]
        C{"nl2sql_reader role:<br/>SELECT on schema ai<br/>and nothing else"}
    end
    C --> P

    subgraph L3["🛡️ Lock 3 · row-level security (database engine)"]
        P{"security.TenantPolicy filters every row<br/>by read-only session context<br/>BrandId · LocationId (0 = whole brand)"}
    end
    P --> OK["📊 Only this tenant's rows<br/>15 s timeout · 500-row cap"]

    classDef model fill:#fde68a,stroke:#b45309,color:#1f2937;
    classDef check fill:#fecaca,stroke:#b91c1c,color:#1f2937;
    classDef good fill:#bbf7d0,stroke:#15803d,color:#1f2937;
    classDef retry fill:#e2e8f0,stroke:#475569,color:#1f2937;
    class SQL model;
    class G,C,P check;
    class OK good;
    class R retry;
```

Tested by `tests/Nl2Sql.UnitTests/SqlGuardTests.cs` (81 evasion attempts) and `tests/Nl2Sql.SafetyTests` (51 tests against the real database, signed in as the production identity).

---

## 2. Who can do what

No passwords or keys exist anywhere: everything signs in with Microsoft Entra ID. ([ADR 0005](adr/0005-entra-only-authentication.md), [ADR 0003](adr/0003-app-service-f1-with-bicep.md))

```mermaid
%%{init: {'theme':'base'}}%%
flowchart LR
    subgraph People["👩‍💻 Developer (az login)"]
        Dev["Entra admin on the database<br/>locally switches to nl2sql_reader_dev<br/>for every generated query"]
    end
    subgraph GH["🐙 GitHub Actions (OIDC)"]
        CIr["safety tests · evals"]
        CId["deploy"]
    end
    subgraph Ids["🪪 Managed identities"]
        Reader["nl2sql-reader"]
        Deployer["nl2sql-deployer"]
    end
    subgraph Res["☁️ Azure resources"]
        App["App Service"]
        DB[("ai views")]
        Fdy["Foundry models"]
    end

    CIr --> Reader
    CId --> Deployer
    App -- "runs as" --> Reader
    Reader -- "SELECT on ai only" --> DB
    Reader -- "OpenAI User" --> Fdy
    Deployer -- "deploy web app + plan,<br/>attach nl2sql-reader" --> App
    Dev -- "migrations, seed" --> DB

    classDef person fill:#fde68a,stroke:#b45309,color:#1f2937;
    classDef gh fill:#e2e8f0,stroke:#475569,color:#1f2937;
    classDef id fill:#ddd6fe,stroke:#6d28d9,color:#1f2937;
    classDef res fill:#bbf7d0,stroke:#15803d,color:#1f2937;
    class Dev person;
    class CIr,CId gh;
    class Reader,Deployer id;
    class App,DB,Fdy res;
```

The deployer can't read data or touch the SQL server or Foundry; the reader can't change anything.

---

## 3. Inside one question

A question passes the gate, then the agent's chat-client pipeline, where each layer adds one concern. ([ADR 0009](adr/0009-personas-and-demo-limits.md))

```mermaid
%%{init: {'theme':'base'}}%%
flowchart TB
    In["POST /api/agui<br/>X-Persona: starbucks-owner"] --> Gate

    subgraph Gate["🚦 QuestionGate (cheapest check first)"]
        direction LR
        g1["known persona?"] --> g2["≤ 500 chars?"] --> g3["quota: 10 / visitor,<br/>200 / day overall"]
    end

    Gate --> Run["AgentRun: persona + scope"]
    Run --> Agent

    subgraph Agent["🤖 Agent pipeline (Microsoft.Extensions.AI)"]
        direction TB
        a1["AgentContextChatClient<br/>who you are · today's date in your time zone"]
        a2["FunctionInvokingChatClient<br/>max 5 model round trips"]
        a3["OpenTelemetry"]
        a4["Responses API · gpt-5.4-mini"]
        a1 --> a2 --> a3 --> a4
    end

    a2 <--> Tools

    subgraph Tools["🧰 Tools (each opens a scoped reader connection)"]
        direction LR
        t1["list_views"] --- t2["describe_view"] --- t3["get_examples"] --- t4["run_query (+ chart)"]
    end

    Agent --> Out["AG-UI event stream →<br/>steps · answer · chart · table · SQL"]

    classDef io fill:#fde68a,stroke:#b45309,color:#1f2937;
    classDef gate fill:#fecaca,stroke:#b91c1c,color:#1f2937;
    classDef agent fill:#ddd6fe,stroke:#6d28d9,color:#1f2937;
    classDef tool fill:#bfdbfe,stroke:#1d4ed8,color:#1f2937;
    class In,Out,Run io;
    class g1,g2,g3 gate;
    class a1,a2,a3,a4 agent;
    class t1,t2,t3,t4 tool;
```

---

## 4. The semantic layer

The model only ever sees the `ai` views. Each encodes business rules so the model never guesses them, and every column has a description stored in the database. ([ADR 0001](adr/0001-semantic-layer-and-discovery-tools.md))

```mermaid
%%{init: {'theme':'base'}}%%
erDiagram
    LOCATIONS ||--o{ ORDERS : "placed at"
    ORDERS ||--|{ ORDER_LINES : contains
    ORDER_LINES ||--o{ ORDER_LINE_MODIFIERS : "milk, syrup, shots"
    MENU_ITEMS ||--o{ ORDER_LINES : "sold as"
    CUSTOMERS ||--o{ ORDERS : "loyalty members"
    ORDERS ||--o{ REFUNDS : "refunded by"

    ORDERS {
        bigint OrderId
        string Channel "mobile · in_store · delivery"
        date LocalDate "location's local time"
        int LocalHour
        decimal NetRevenue "after refunds, excl. tax and tips"
        bit IsLoyaltyMember
    }
    ORDER_LINES {
        string MenuItemName
        bit IsLatte "hot and iced"
        bit IsCoffee "espresso + brewed"
        string MilkType
        int Quantity
        decimal LineNetRevenue
    }
    LOCATIONS {
        string LocationName
        string TimeZone
        char Currency "CAD · USD"
    }
```

Voided and cancelled orders are already excluded, and the same views carry row-level security from the base tables beneath them.

---

## 5. From commit to live site

```mermaid
%%{init: {'theme':'base'}}%%
flowchart LR
    Push["git push main"] --> CI

    subgraph CI["✅ ci.yml"]
        direction TB
        c1["dotnet: locked restore · format ·<br/>analyzers · build · 128 unit tests"]
        c2["safety: 51 tests vs real DB<br/>as nl2sql-reader (OIDC)"]
        c3["web: vp check · test · build"]
    end

    CI -- "all green" --> Deploy

    subgraph Deploy["🚀 deploy.yml (as nl2sql-deployer)"]
        direction TB
        d1["build React · publish API<br/>(self-contained, RC2)"]
        d2["Bicep: F1 plan + web app"]
        d3["zip deploy · start"]
        d4["smoke test: ask a real question"]
        d1 --> d2 --> d3 --> d4
    end

    Deploy --> Live(["🌐 ms-sql-nl2sql-agent.azurewebsites.net"])
    Manual["🖐️ evals.yml (manual)"] -. "26 golden cases × models × modes" .-> Report["📊 job summary"]

    classDef step fill:#bfdbfe,stroke:#1d4ed8,color:#1f2937;
    classDef dep fill:#ddd6fe,stroke:#6d28d9,color:#1f2937;
    classDef live fill:#bbf7d0,stroke:#15803d,color:#1f2937;
    classDef manual fill:#fde68a,stroke:#b45309,color:#1f2937;
    class c1,c2,c3 step;
    class d1,d2,d3,d4 dep;
    class Live live;
    class Manual,Report manual;
```

Database schema changes (the dacpac and seed) are deliberately applied from a developer's machine, not from CI: the database is shared and free-tier, and schema changes are rare.

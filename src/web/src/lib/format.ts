const integerFormat = new Intl.NumberFormat(undefined, { maximumFractionDigits: 0 });
const decimalFormat = new Intl.NumberFormat(undefined, {
  minimumFractionDigits: 2,
  maximumFractionDigits: 2,
});
const dateFormat = new Intl.DateTimeFormat(undefined, { dateStyle: "medium" });
const monthFormat = new Intl.DateTimeFormat(undefined, { month: "short", year: "numeric" });
const dateTimeFormat = new Intl.DateTimeFormat(undefined, {
  dateStyle: "medium",
  timeStyle: "short",
});

const isoDateTime = /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}(:\d{2}(\.\d+)?)?$/;

/** Formats a result cell for people: thousands separators, readable dates, an em dash for null. */
export function formatCell(
  value: string | number | boolean | null,
  options: { months?: boolean } = {},
): string {
  if (value === null) return "—";
  if (typeof value === "number")
    return (Number.isInteger(value) ? integerFormat : decimalFormat).format(value);
  if (typeof value === "boolean") return value ? "Yes" : "No";
  if (isoDateTime.test(value)) {
    const date = new Date(value);
    if (options.months) return monthFormat.format(date);
    const isMidnight = value.slice(11, 19) === "00:00:00";
    return isMidnight ? dateFormat.format(date) : dateTimeFormat.format(date);
  }
  return value;
}

/** Splits PascalCase column names into words: "NetRevenue" → "Net Revenue". */
export function columnLabel(column: string): string {
  return column.replace(/([a-z0-9])([A-Z])/g, "$1 $2");
}

/** True when every value is a date on the 1st of a month: a "month" column, shown as "Sep 2026". */
export function isMonthColumn(values: ReadonlyArray<string | number | boolean | null>): boolean {
  return (
    values.length > 0 &&
    values.every(
      (v) => typeof v === "string" && isoDateTime.test(v) && v.slice(8, 19) === "01T00:00:00",
    )
  );
}

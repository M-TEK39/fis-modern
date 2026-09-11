export function formatDate(value: string | null) {
  return value ? value.slice(0, 10) : "-";
}

export function formatCurrency(value: number | null) {
  return value === null
    ? "-"
    : `R ${value.toLocaleString("en-ZA", { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
}

export function valueOrDash(value: string | number | null | undefined) {
  return value === null || value === undefined || String(value).trim() === "" ? "-" : String(value);
}

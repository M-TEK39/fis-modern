const LOGSHEET_NUMBER_FORMATTER = new Intl.NumberFormat("en-ZA", { maximumFractionDigits: 2 });

export function valueOrDash(value: string | number | null | undefined) {
  return value === null || value === undefined || String(value).trim() === "" ? "-" : String(value);
}

export function formatDate(value: string | null | undefined) {
  return value?.slice(0, 10) || "-";
}

export function formatNumber(value: number) {
  return LOGSHEET_NUMBER_FORMATTER.format(value);
}

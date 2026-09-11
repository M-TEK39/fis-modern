export function valueOrDash(value: string | number | null | undefined) {
  return value === null || value === undefined || String(value).trim() === "" ? "-" : String(value);
}

export function formatDate(value: string | null | undefined) {
  return value?.slice(0, 10) || "-";
}

export function queryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? (value[0] ?? "") : (value ?? "");
}

export function taxiPageHref(
  path: string,
  query: Record<string, string | string[] | undefined>,
  page: number,
) {
  const params = new URLSearchParams();
  for (const [key, value] of Object.entries(query)) {
    if (key === "page" || value === undefined) continue;
    const item = Array.isArray(value) ? value[0] : value;
    if (item) params.set(key, item);
  }
  if (page > 1) params.set("page", String(page));
  const search = params.toString();
  return search ? `${path}?${search}` : path;
}

export function valueOrDash(value: string | number | null | undefined) {
  return value === null || value === undefined || (typeof value === "string" && !value.trim())
    ? "-"
    : String(value);
}

export function dateValue(value: string | null | undefined) {
  if (!value) return "-";
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? value : date.toLocaleDateString();
}

export function timeValue(value: string | null | undefined) {
  if (!value) return "-";
  const match = value.match(/T(\d{2}:\d{2})/);
  return match?.[1] ?? value.slice(0, 5);
}

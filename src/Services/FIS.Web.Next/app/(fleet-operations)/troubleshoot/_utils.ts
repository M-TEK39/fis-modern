export const TROUBLESHOOTING_ROLE = "Trouble Shooting";

export function hasTroubleshootingRole(roles: readonly string[]) {
  return roles.some(
    (role) => role.localeCompare(TROUBLESHOOTING_ROLE, undefined, { sensitivity: "accent" }) === 0,
  );
}

export function valueOrDash(value: string | number | null | undefined) {
  return value === null || value === undefined || String(value).trim() === "" ? "-" : String(value);
}

export function dateValue(value: string | null | undefined) {
  return value ? value.slice(0, 10) : "-";
}

export function pageNumber(value: string | string[] | undefined) {
  const parsed = Number(Array.isArray(value) ? value[0] : value);
  return Number.isSafeInteger(parsed) && parsed > 0 ? parsed : 1;
}

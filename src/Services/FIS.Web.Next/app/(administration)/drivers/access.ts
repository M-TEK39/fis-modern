export function hasVehicleManagementPermission(
  roles: readonly string[],
  expectedRole: "Vehicle Master" | "Validation" = "Vehicle Master",
) {
  return hasRole(roles, expectedRole);
}

export function hasLegacyRole(roles: readonly string[], role: string) {
  return hasRole(roles, role);
}

export function hasDriverAuthoriserManagementRole(roles: readonly string[]) {
  return hasSystemAdministratorRole(roles) || hasRole(roles, "Driver and Authoriser Management");
}

export function hasRole(roles: readonly string[], role: string) {
  return roles.some(
    (candidate) => candidate.localeCompare(role, undefined, { sensitivity: "accent" }) === 0,
  );
}

function hasSystemAdministratorRole(roles: readonly string[]) {
  return roles.some((candidate) =>
    ["systemadministrator", "system administrator"].includes(candidate.trim().toLowerCase()),
  );
}

export function getQueryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

export function parsePositiveInteger(value: string | undefined) {
  const parsed = Number(value);
  return value && Number.isSafeInteger(parsed) && parsed > 0 ? parsed : null;
}

export function contextQuery(departmentCode: number, siteCode: number) {
  return new URLSearchParams({
    departmentCode: String(departmentCode),
    siteCode: String(siteCode),
  });
}

export function contextPath(path: string, departmentCode: number, siteCode: number) {
  return `${path}?${contextQuery(departmentCode, siteCode).toString()}`;
}

export function actionResultPath(
  path: string,
  departmentCode: number,
  siteCode: number,
  result: string,
) {
  const query = contextQuery(departmentCode, siteCode);
  query.set("result", result);
  return `${path}?${query.toString()}`;
}

export const VEHICLE_MANAGEMENT_PERMISSION = 1;

export function hasVehicleManagementPermission(accessLevel?: string) {
  if (!accessLevel) {
    return false;
  }

  try {
    return (BigInt(accessLevel) & BigInt(VEHICLE_MANAGEMENT_PERMISSION)) === BigInt(VEHICLE_MANAGEMENT_PERMISSION);
  } catch {
    return false;
  }
}

export function hasRole(roles: readonly string[], role: string) {
  return roles.some((candidate) => candidate.localeCompare(role, undefined, { sensitivity: "accent" }) === 0);
}

export function getQueryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

export function parsePositiveInteger(value: string | undefined) {
  const parsed = Number(value);
  return value && Number.isSafeInteger(parsed) && parsed > 0 ? parsed : null;
}

export function contextQuery(departmentCode: number, siteCode: number) {
  return new URLSearchParams({ departmentCode: String(departmentCode), siteCode: String(siteCode) });
}

export function contextPath(path: string, departmentCode: number, siteCode: number) {
  return `${path}?${contextQuery(departmentCode, siteCode).toString()}`;
}

export function actionResultPath(path: string, departmentCode: number, siteCode: number, result: string) {
  const query = contextQuery(departmentCode, siteCode);
  query.set("result", result);
  return `${path}?${query.toString()}`;
}

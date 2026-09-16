const TAXI_ACCESS_ROLES = new Set([
  "private hire vehicles",
  "taxi information maintenance",
  "taxi information maintenance",
  "systemadministrator",
  "system administrator",
]);

const RECURRING_TAXI_ROLES = new Set([
  "book recurring taxi",
  "book recuring taxi",
  "systemadministrator",
  "system administrator",
]);

export function hasTaxiAccess(roles: readonly string[]) {
  return roles.some((role) => TAXI_ACCESS_ROLES.has(role.trim().toLowerCase()));
}

export function hasRecurringTaxiAccess(roles: readonly string[]) {
  return roles.some((role) => RECURRING_TAXI_ROLES.has(role.trim().toLowerCase()));
}

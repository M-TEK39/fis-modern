const FINE_ACCESS_ROLES = new Set([
  "reports",
  "systemadministrator",
  "system administrator",
]);

/**
 * The legacy Fines pages are protected by the Reports role. A legacy system
 * administrator is intentionally expanded to the module roles by the API,
 * and must receive the same access in the server-rendered route guards.
 */
export function hasFinesAccess(roles: readonly string[]) {
  return roles.some((role) => FINE_ACCESS_ROLES.has(role.trim().toLocaleLowerCase()));
}

export const hasReportsRole = hasFinesAccess;

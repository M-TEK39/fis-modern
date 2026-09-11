const FINANCE_ROLES = [
  "financial reports",
  "financial data (own department)",
  "financial data (all departments)",
  "administrator",
  "admin",
] as const;

const FINANCE_ROLE_SET = new Set<string>(FINANCE_ROLES);

export function hasFinanceRole(roles: readonly string[]) {
  return roles.some((role) => FINANCE_ROLE_SET.has(role.trim().toLowerCase()));
}

export function hasRole(roles: readonly string[], role: string) {
  const expected = role.trim().toLowerCase();
  return roles.some((item) => item.trim().toLowerCase() === expected);
}

export function hasCoisIdentity(email: string | undefined, roles: readonly string[]) {
  const username = email?.split("@", 1)[0]?.trim().toLowerCase();
  return username === "cois" || hasRole(roles, "COIS");
}

export function hasHeadOfficeFinanceAccess(
  siteCode: string | undefined,
  email: string | undefined,
  roles: readonly string[],
) {
  return siteCode === "1598" || hasCoisIdentity(email, roles);
}

export function hasTariffParametersRole(roles: readonly string[]) {
  return hasRole(roles, "Financial Tariff Parameters") || hasTariffApproverRole(roles);
}

export function hasTariffApproverRole(roles: readonly string[]) {
  return hasRole(roles, "Financial Tariff Parameters (Approver)");
}

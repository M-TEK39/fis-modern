const FINANCE_ROLES = [
  "financial reports",
  "financial data (own department)",
  "financial data (all departments)",
  "administrator",
  "admin",
  "systemadministrator",
  "system administrator",
] as const;

const FINANCE_ROLE_SET = new Set<string>(FINANCE_ROLES);

export function hasFinanceRole(roles: readonly string[]) {
  return roles.some((role) => FINANCE_ROLE_SET.has(role.trim().toLowerCase()));
}

export function hasFinanceDataMaintenanceRole(roles: readonly string[]) {
  return (
    hasAdministratorRole(roles) ||
    hasRole(roles, "Financial Data (Own Department)") ||
    hasRole(roles, "Financial Data (All Departments)")
  );
}

export function hasFinancialReportsRole(roles: readonly string[]) {
  return (
    hasAdministratorRole(roles) ||
    hasRole(roles, "Financial Reports")
  );
}

export function canMaintainAllFinanceData(
  roles: readonly string[],
  hasProvinceWideVehicleListRole = false,
) {
  return (
    hasAdministratorRole(roles) ||
    (hasRole(roles, "Financial Data (All Departments)") && hasProvinceWideVehicleListRole)
  );
}

export function canSelectBasCorrectionDepartments(
  roles: readonly string[],
  siteCode: string | undefined,
  legacyUsername: string | undefined,
) {
  return (
    hasAdministratorRole(roles) ||
    (hasRole(roles, "Financial Data (All Departments)") &&
      hasHeadOfficeFinanceAccess(siteCode, legacyUsername))
  );
}

export function hasBasCorrectionRole(roles: readonly string[]) {
  return (
    hasAdministratorRole(roles) ||
    hasRole(roles, "Financial Data (Own Department)") ||
    hasRole(roles, "Financial Data (All Departments)") ||
    hasRole(roles, "Vehicle List for All Sites in Department")
  );
}

export function hasRole(roles: readonly string[], role: string) {
  const expected = role.trim().toLowerCase();
  return roles.some((item) => item.trim().toLowerCase() === expected);
}

export function hasAdministratorRole(roles: readonly string[]) {
  return (
    hasRole(roles, "Administrator") ||
    hasRole(roles, "Admin") ||
    hasRole(roles, "SystemAdministrator") ||
    hasRole(roles, "System Administrator")
  );
}

export function canSelectAllFinanceDepartments(
  roles: readonly string[],
  hasVerifiedAllDepartmentRole = false,
  _accessLevel?: string,
) {
  return (
    hasAdministratorRole(roles) ||
    (hasRole(roles, "Financial Reports") && hasVerifiedAllDepartmentRole)
  );
}

export function hasCoisIdentity(legacyUsername: string | undefined) {
  return legacyUsername?.trim().toLowerCase() === "cois";
}

export function hasHeadOfficeFinanceAccess(
  siteCode: string | undefined,
  legacyUsername: string | undefined,
) {
  return siteCode === "1598" || hasCoisIdentity(legacyUsername);
}

export function hasAdvancedBatchOperationsRole(roles: readonly string[]) {
  return hasRole(roles, "Advanced Financial Operations - Batch");
}

export function hasGeneralFinanceReportsAccess(roles: readonly string[]) {
  return hasFinanceRole(roles) || hasRole(roles, "Reports");
}

export function hasAuditTrailReportsAccess(roles: readonly string[]) {
  return hasAdministratorRole(roles) || hasRole(roles, "Reports");
}

export function hasTariffParametersRole(roles: readonly string[]) {
  return hasRole(roles, "Financial Tariff Parameters") || hasTariffApproverRole(roles);
}

export function hasTariffApproverRole(roles: readonly string[]) {
  return hasRole(roles, "Financial Tariff Parameters (Approver)");
}

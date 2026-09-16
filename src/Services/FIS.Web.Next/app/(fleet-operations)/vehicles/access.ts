export const VEHICLE_MASTER_ROLE = "Vehicle Master";
export const VEHICLE_INCEPTION_CAPTURER_ROLE = "Vehicle Inception Capturer";
export const VEHICLE_INCEPTION_AUTHORIZER_ROLE = "Vehicle Inception Authorizer";
const SYSTEM_ADMINISTRATOR_ROLES = [
  "SystemAdministrator",
  "System Administrator",
] as const;

export const VEHICLE_STATUS_ROLES = ["Acquisition", "Logistics", "TSS", "Workshop"] as const;

function hasExactRole(roles: readonly string[], role: string) {
  return roles.some(
    (candidate) => candidate.localeCompare(role, undefined, { sensitivity: "accent" }) === 0,
  );
}

export function hasRole(roles: readonly string[], role: string) {
  return hasSystemAdministratorRole(roles) || hasExactRole(roles, role);
}

export function hasVehicleMasterRole(roles: readonly string[]) {
  return hasRole(roles, VEHICLE_MASTER_ROLE);
}

export function hasVehicleInceptionCapturerRole(roles: readonly string[]) {
  return hasRole(roles, VEHICLE_INCEPTION_CAPTURER_ROLE);
}

export function hasVehicleInceptionAuthorizerRole(roles: readonly string[]) {
  return hasRole(roles, VEHICLE_INCEPTION_AUTHORIZER_ROLE);
}

function hasSystemAdministratorRole(roles: readonly string[]) {
  return SYSTEM_ADMINISTRATOR_ROLES.some((role) => hasExactRole(roles, role));
}

export function hasVehicleStatusRole(roles: readonly string[]) {
  return hasSystemAdministratorRole(roles) || VEHICLE_STATUS_ROLES.some((role) => hasRole(roles, role));
}

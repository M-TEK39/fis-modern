export const DEMO_VEHICLES_ROLE = "Demo Vehicles";

export function hasDemoVehicleRole(roles: readonly string[]) {
  return roles.some((role) => role.localeCompare(DEMO_VEHICLES_ROLE, undefined, { sensitivity: "base" }) === 0);
}

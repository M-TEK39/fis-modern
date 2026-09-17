export function hasAssetVerificationAccess(roles: readonly string[]) {
  return roles.some(
    (role) =>
      role.localeCompare("Asset Verification", undefined, { sensitivity: "accent" }) === 0,
  );
}

export function getQueryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

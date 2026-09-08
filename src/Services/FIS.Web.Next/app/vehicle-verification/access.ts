export function hasAssetVerificationAccess(roles: readonly string[], accessLevel?: string) {
  if (
    roles.some(
      (role) =>
        role.localeCompare("Asset Verification", undefined, { sensitivity: "accent" }) === 0,
    )
  ) {
    return true;
  }

  if (!accessLevel) return false;
  try {
    return (BigInt(accessLevel) & BigInt(1)) === BigInt(1);
  } catch {
    return false;
  }
}

export function getQueryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

export const NOTICE_MANAGEMENT_PERMISSION = 4;

export function hasNoticeManagementPermission(accessLevel?: string) {
  if (!accessLevel) return false;

  try {
    return (
      (BigInt(accessLevel) & BigInt(NOTICE_MANAGEMENT_PERMISSION)) ===
      BigInt(NOTICE_MANAGEMENT_PERMISSION)
    );
  } catch {
    return false;
  }
}

export function getQueryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

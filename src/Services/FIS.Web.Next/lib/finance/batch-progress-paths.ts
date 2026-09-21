export function isBatchProgressExemptPath(pathname: string) {
  const path = pathname.trim().toLowerCase();
  if (!path) return true;
  if (path === "/finance" || path.startsWith("/finance/")) return true;
  if (path === "/batch-in-progress" || path.startsWith("/batch-in-progress/")) return true;
  if (path.includes("batchinprogress")) return true;
  if (path.includes("downloadreport")) return true;
  if (
    path.startsWith("/login") ||
    path.startsWith("/forgot-password") ||
    path.startsWith("/reset-password") ||
    path.startsWith("/change-password") ||
    path.startsWith("/signedout") ||
    path.startsWith("/terms")
  )
    return true;
  if (
    path.startsWith("/_next") ||
    path.startsWith("/logo/") ||
    path.startsWith("/images/") ||
    path.startsWith("/favicon") ||
    path.startsWith("/scripts/") ||
    path.startsWith("/jscripts/")
  )
    return true;
  return false;
}

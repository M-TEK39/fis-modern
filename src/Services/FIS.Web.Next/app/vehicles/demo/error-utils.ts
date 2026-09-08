export function isUnauthorizedError(error: unknown) {
  return (
    typeof error === "object" &&
    error !== null &&
    "reason" in error &&
    (error as { reason?: unknown }).reason === "unauthorized"
  );
}

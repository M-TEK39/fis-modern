import type { JobCardRecord } from "@/lib/api/fleet-operations/api-job-cards";

const JOB_CARD_MONEY_FORMATTER = new Intl.NumberFormat("en-ZA", {
  style: "currency",
  currency: "ZAR",
});

export function valueOrDash(value: string | number | null | undefined) {
  return value === null || value === undefined || String(value).trim() === "" ? "-" : String(value);
}

export function formatDate(value: string | null | undefined) {
  return value?.slice(0, 10) || "-";
}

export function formatDateTimeInput(value: string | null | undefined) {
  return value?.slice(0, 10) || "";
}

export function formatMoney(value: number | null | undefined) {
  return value === null || value === undefined ? "-" : JOB_CARD_MONEY_FORMATTER.format(value);
}

export function statusLabel(card: Pick<JobCardRecord, "statusCode" | "statusText">) {
  return (
    card.statusText ||
    ({
      0: "Pending review",
      1: "Pending",
      2: "Awaiting authorization",
      3: "Authorized",
      4: "In progress",
      5: "Complete",
      6: "Failed",
      7: "Canceled",
    }[card.statusCode] ??
      "Unknown")
  );
}

export function hasRole(roles: readonly string[], kind: "capturer" | "authorizer") {
  return roles.some((role) => {
    const normalized = role.toLocaleLowerCase().replace(/[^a-z0-9]/g, "");
    return (
      normalized.includes("jobcard") &&
      normalized.includes(kind === "capturer" ? "captur" : "author")
    );
  });
}

export function hasJobCardAccess(accessLevel: string | undefined, roles: readonly string[]) {
  const numericAccessLevel = Number(accessLevel);
  return (
    hasRole(roles, "capturer") ||
    hasRole(roles, "authorizer") ||
    (Number.isInteger(numericAccessLevel) && (numericAccessLevel & (1 | 32)) !== 0)
  );
}

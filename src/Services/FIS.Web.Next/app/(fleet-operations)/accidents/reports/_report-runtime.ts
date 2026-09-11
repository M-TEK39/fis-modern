import "server-only";

import { redirect } from "next/navigation";
import { connection } from "next/server";

import { AccidentApiError } from "@/lib/api/fleet-operations/api-accidents";
import { getSession } from "@/lib/auth/session";

const ACCIDENTS_ROLE = "Accidents";

export type AccidentReportAuthorization = "authorized" | "expired" | "unavailable" | "forbidden";

export async function authorizeAccidentReport(): Promise<AccidentReportAuthorization> {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") {
    redirect("/login");
  }
  if (session.status === "expired") {
    return "expired";
  }
  if (session.status === "unavailable") {
    return "unavailable";
  }
  if (
    !session.roles.some(
      (candidate) =>
        candidate.localeCompare(ACCIDENTS_ROLE, undefined, { sensitivity: "accent" }) === 0,
    )
  ) {
    return "forbidden";
  }

  return "authorized";
}

type ReportLoadResult<T> =
  | { status: "not-requested"; data: null }
  | { status: "success"; data: T }
  | { status: "unauthorized" | "error"; data: null };

export async function loadAccidentReport<T>({
  shouldRun,
  errorMessage,
  load,
  context,
  isUnauthorized = (error) => error instanceof AccidentApiError && error.reason === "unauthorized",
}: Readonly<{
  shouldRun: boolean;
  errorMessage: string | null;
  load: () => Promise<T>;
  context: string;
  isUnauthorized?: (error: unknown) => boolean;
}>): Promise<ReportLoadResult<T>> {
  if (!shouldRun || errorMessage) {
    return { status: "not-requested", data: null };
  }

  try {
    return { status: "success", data: await load() };
  } catch (error) {
    if (isUnauthorized(error)) {
      return { status: "unauthorized", data: null };
    }

    console.error(context, error instanceof Error ? error.message : "unknown error");
    return { status: "error", data: null };
  }
}

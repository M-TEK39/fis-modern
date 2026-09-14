import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import {
  FinanceFrame,
  FinanceRestricted,
  FinanceUnavailable,
} from "@/app/(fleet-operations)/finance/_components";
import { hasFinanceRole, hasRole } from "@/app/(fleet-operations)/finance/_utils";
import { getSession } from "@/lib/auth/session";

type Query = Record<string, string | string[] | undefined>;

const AVAILABLE_DYNAMIC_REPORTS = new Set([
  "9.2 all vehicle statuses",
  "9.3 report to show incorrect calculated quantities",
]);

function queryValue(query: Query, name: string) {
  const value = query[name];
  return Array.isArray(value) ? (value[0] ?? "") : (value ?? "");
}

/**
 * GeneratedReports.aspx was a file catalogue, not a reporting query. Its
 * original Excel files are absent from both the legacy source archive and the
 * modern deployment assets. The two reports which have a dynamic compatibility
 * implementation remain available; the other catalogue files remain an
 * explicit deployment dependency instead of a misleading empty download.
 */
export default async function LegacyGeneratedReportsPage({
  searchParams,
}: Readonly<{ searchParams: Promise<Query> }>) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status !== "authenticated")
    return (
      <FinanceFrame title="Pre-Generated Reports" description="Reports available for download.">
        <FinanceUnavailable message="The sign-in service is temporarily unavailable. Please try again." />
      </FinanceFrame>
    );
  if (!hasRole(session.roles, "Reports") || !hasFinanceRole(session.roles))
    return (
      <FinanceFrame title="Pre-Generated Reports" description="Reports available for download.">
        <FinanceRestricted message="The legacy catalogue required both Reports and Financial Reports permission." />
      </FinanceFrame>
    );

  const query = await searchParams;
  const key = queryValue(query, "key").trim().toLowerCase();
  if (AVAILABLE_DYNAMIC_REPORTS.has(key))
    redirect(
      key.startsWith("9.2") ? "/reports/vehicle-status-all" : "/reports/incorrect-quantities",
    );

  return (
    <FinanceFrame title="Pre-Generated Reports" description="Reports available for download.">
      <section className="vehicle-status-card">
        <h2>Reports available for download</h2>
        <div className="button-row">
          <Link className="button button-primary" href="/reports/vehicle-status-all">
            All Vehicle Statuses
          </Link>
          <Link className="button button-primary" href="/reports/incorrect-quantities">
            Incorrect Calculated Quantities
          </Link>
        </div>
        <p className="muted-copy">
          The legacy monthly utilisation workbooks require their original ExcelGeneratedReports
          deployment files. They are not in this repository or its legacy source archive.
        </p>
      </section>
    </FinanceFrame>
  );
}

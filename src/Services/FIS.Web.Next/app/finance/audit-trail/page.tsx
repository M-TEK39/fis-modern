import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import { FinanceFrame, FinanceMenuLink, FinanceMenuSection, FinanceRestricted, FinanceUnavailable, hasFinanceRole } from "@/app/finance/_components";
import { getSession } from "@/lib/session";

export default async function FinanceAuditTrailPage() {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status !== "authenticated") return <FinanceFrame title="Audit Trail Reports" description="Audit trail reporting."><FinanceUnavailable message="The sign-in service is temporarily unavailable. Please try again." /></FinanceFrame>;
  if (!hasFinanceRole(session.roles)) return <FinanceFrame title="Audit Trail Reports" description="Audit trail reporting."><FinanceRestricted /></FinanceFrame>;
  return <FinanceFrame title="Audit Trail Reports" description="Audit trail reporting."><div className="vehicle-menu-tiles"><FinanceMenuSection title="Audit Trail Report Menu"><FinanceMenuLink href="/finance/audit-trail/department">1) Audit Trail grouped by Department in a date range</FinanceMenuLink><FinanceMenuLink href="/finance/audit-trail/site">2) Audit Trail grouped by Site in a date range</FinanceMenuLink><FinanceMenuLink href="/finance/audit-trail/vehicle">3) Audit Trail grouped per Vehicle in a date range</FinanceMenuLink><Link className="vehicle-menu-link" href="/finance">4) Return To Main Page</Link></FinanceMenuSection></div></FinanceFrame>;
}

import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import { FinanceFrame, FinanceMenuLink, FinanceMenuSection, FinanceRestricted, FinanceUnavailable, hasFinanceRole } from "@/app/finance/_components";
import { getSession } from "@/lib/session";

export default async function WesbankMenuPage() {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status !== "authenticated") return <FinanceFrame title="Wesbank Expenses Reports" description="Wesbank expense reporting."><FinanceUnavailable message="The sign-in service is temporarily unavailable. Please try again." /></FinanceFrame>;
  if (!hasFinanceRole(session.roles)) return <FinanceFrame title="Wesbank Expenses Reports" description="Wesbank expense reporting."><FinanceRestricted /></FinanceFrame>;

  return <FinanceFrame title="Wesbank Expenses Reports" description="Wesbank expense reporting.">
    <FinanceMenuSection title="Wesbank Expenses Reports">
      <FinanceMenuLink href="/finance/wesbank/summary-all">a) Summary Expenses Reports (All Inclusive)</FinanceMenuLink>
      <FinanceMenuLink href="/finance/wesbank/summary-selection">b) Summary Expenses Reports (Selection)</FinanceMenuLink>
      <FinanceMenuLink href="/finance/wesbank/detailed-all">c) Detailed Expenses Reports (All Inclusive)</FinanceMenuLink>
      <FinanceMenuLink href="/finance/wesbank/detailed-selection">d) Detailed Expenses Reports (Selection)</FinanceMenuLink>
    </FinanceMenuSection>
    <div className="vehicle-footer-actions"><Link className="button button-secondary" href="/finance">Finance Menu</Link></div>
  </FinanceFrame>;
}

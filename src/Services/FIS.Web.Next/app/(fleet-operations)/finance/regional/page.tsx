import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import {
  FinanceFrame,
  FinanceMenuLink,
  FinanceMenuSection,
  FinanceRestricted,
  FinanceUnavailable,
  hasFinanceRole,
} from "@/app/(fleet-operations)/finance/_components";
import { getSession } from "@/lib/auth/session";

export default async function RegionalFinanceMenuPage() {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status !== "authenticated")
    return (
      <FinanceFrame title="Regional Module: Finance" description="Regional finance reporting.">
        <FinanceUnavailable message="The sign-in service is temporarily unavailable. Please try again." />
      </FinanceFrame>
    );
  if (!hasFinanceRole(session.roles))
    return (
      <FinanceFrame title="Regional Module: Finance" description="Regional finance reporting.">
        <FinanceRestricted />
      </FinanceFrame>
    );

  return (
    <FinanceFrame title="Regional Module: Finance" description="Regional finance reporting.">
      <FinanceMenuSection title="Regional Module: Finance">
        <FinanceMenuLink href="/finance/regional/summary-per-province">
          a) Summary Invoice Reports (Per Province)
        </FinanceMenuLink>
        <FinanceMenuLink href="/finance/regional/summary-all">
          b) Summary Invoice Reports (All)
        </FinanceMenuLink>
        <FinanceMenuLink href="/finance/reports/department">
          c) Print Financial Reports by Department
        </FinanceMenuLink>
        <FinanceMenuLink href="/finance/reports/site">
          d) Print Financial Reports by Site
        </FinanceMenuLink>
        <FinanceMenuLink href="/finance/reports/vehicle-billing-history">
          e) Vehicle Billing History (i.e. vehicle Income)
        </FinanceMenuLink>
        <FinanceMenuLink href="/finance/wesbank/summary-all">
          f) Wesbank Expenses Reports
        </FinanceMenuLink>
      </FinanceMenuSection>
      <div className="vehicle-footer-actions">
        <Link className="button button-secondary" href="/finance">
          Finance Menu
        </Link>
      </div>
    </FinanceFrame>
  );
}

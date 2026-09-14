import { Suspense } from "react";

import RouteLoading from "@/components/app-shell/route-loading";

import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";
import {
  FinanceFrame,
  FinanceMenuLink,
  FinanceMenuSection,
  FinanceRestricted,
  FinanceUnavailable,
} from "@/app/(fleet-operations)/finance/_components";
import { hasRole } from "@/app/(fleet-operations)/finance/_utils";
import { getSession } from "@/lib/auth/session";

async function RegionalAssetMenuContent() {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status !== "authenticated")
    return (
      <FinanceFrame title="Asset List Reports" description="New and in-service vehicle reports.">
        <FinanceUnavailable message="The sign-in service is temporarily unavailable. Please try again." />
      </FinanceFrame>
    );
  if (
    !hasRole(session.roles, "Reports") &&
    !hasRole(session.roles, "Administrator") &&
    !hasRole(session.roles, "Admin")
  )
    return (
      <FinanceFrame title="Asset List Reports" description="New and in-service vehicle reports.">
        <FinanceRestricted />
      </FinanceFrame>
    );

  return (
    <FinanceFrame title="Asset List Reports" description="All new and in-service vehicles.">
      <FinanceMenuSection title="Asset List: All Vehicles Reports New & In-Service">
        <FinanceMenuLink href="/finance/regional/assets-all">
          i) Asset List: All Vehicles in FIS with status New &amp; In-Service (All Departments)
        </FinanceMenuLink>
        <FinanceMenuLink href="/finance/regional/assets-province">
          ii) Asset List: All Vehicles in FIS with status New &amp; In-Service (View by Province)
        </FinanceMenuLink>
        <FinanceMenuLink href="/finance/regional/assets-department">
          iii) Asset List: All Vehicles in FIS with status New &amp; In-Service (View by Department)
        </FinanceMenuLink>
        <FinanceMenuLink href="/finance/regional/assets-site">
          iv) Asset List: All Vehicles in FIS with status New &amp; In-Service (View by Site)
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

export default function RegionalAssetMenuPage() {
  return (
    <Suspense fallback={<RouteLoading />}>
      <RegionalAssetMenuContent />
    </Suspense>
  );
}

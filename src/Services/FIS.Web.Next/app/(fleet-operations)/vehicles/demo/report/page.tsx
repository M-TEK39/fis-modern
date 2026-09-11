import Link from "next/link";
import { connection } from "next/server";
import { redirect } from "next/navigation";
import { Suspense } from "react";

import { hasDemoVehicleRole } from "@/app/(fleet-operations)/vehicles/demo/access";
import DemoVehicleReport from "@/app/(fleet-operations)/vehicles/demo/demo-vehicle-report";
import {
  DemoAccessRestricted,
  DemoApiUnavailable,
  DemoSessionRecovery,
} from "@/app/(fleet-operations)/vehicles/demo/page-support";
import RouteLoading from "@/components/app-shell/route-loading";
import { getSession } from "@/lib/auth/session";

async function DemoReportPageContent() {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired")
    return (
      <main className="page-shell vehicle-page-shell">
        <DemoSessionRecovery returnPath="/vehicles/demo/report" />
      </main>
    );
  if (session.status === "unavailable")
    return (
      <main className="page-shell vehicle-page-shell">
        <DemoApiUnavailable message="The sign-in service is temporarily unavailable." />
      </main>
    );
  if (!hasDemoVehicleRole(session.roles))
    return (
      <main className="page-shell vehicle-page-shell">
        <DemoAccessRestricted message="You do not have permission to view demo vehicle reports." />
      </main>
    );

  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-labelledby="demo-report-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Vehicle Master / Demo Vehicles</p>
            <h1 id="demo-report-title">Report: All Demo Vehicles</h1>
            <p>Review the same nine report columns as the legacy report.</p>
          </div>
          <div className="button-row">
            <Link className="button button-secondary" href="/vehicles">
              Vehicle Master
            </Link>
          </div>
        </header>
        <DemoVehicleReport />
      </section>
    </main>
  );
}

export default function DemoReportPage() {
  return (
    <Suspense fallback={<RouteLoading />}>
      <DemoReportPageContent />
    </Suspense>
  );
}

import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";
import { Suspense } from "react";

import { ReportsFrame } from "@/app/(fleet-operations)/reports/_components";
import RouteLoading from "@/components/app-shell/route-loading";
import { getSession } from "@/lib/auth/session";

async function TariffCalculatorContent() {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status !== "authenticated")
    return (
      <ReportsFrame
        title="Tariff Calculator"
        description="The tariff workspace is temporarily unavailable."
      >
        <p className="notice notice-error">
          The FIS API could not be reached. Retry when it is available.
        </p>
      </ReportsFrame>
    );

  return (
    <ReportsFrame
      title="Tariff Calculator"
      description="Legacy tariff calculator entry point routed to the modern tariff workflow."
      backHref="/home"
      backLabel="Main Menu"
    >
      <section className="vehicle-status-maintenance-panel">
        <p>Use the modern tariff screens below:</p>
        <div className="button-row">
          <Link className="button button-primary" href="/full-maintenance-lease/tariffs">
            Tariff Capture
          </Link>
          <Link className="button button-secondary" href="/reports/tariffs">
            Tariff Reports
          </Link>
        </div>
      </section>
    </ReportsFrame>
  );
}

export default function TariffCalculatorPage() {
  return (
    <Suspense fallback={<RouteLoading />}>
      <TariffCalculatorContent />
    </Suspense>
  );
}

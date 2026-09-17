import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";
import { Suspense } from "react";
import RouteLoading from "@/components/app-shell/route-loading";

import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import {
  hasVehicleInceptionAuthorizerRole,
  hasVehicleInceptionCapturerRole,
} from "@/app/(fleet-operations)/vehicles/access";
import { getSession } from "@/lib/auth/session";

function EntryUnavailable() {
  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-status-card" role="alert">
        <div className="status-icon status-icon-error" aria-hidden="true">
          !
        </div>
        <p className="eyebrow">API unavailable</p>
        <h2>Your vehicle workflow could not be opened.</h2>
        <p className="muted-copy">Retry when the FIS API is available.</p>
        <Link className="button button-primary" href="/login">
          Sign in
        </Link>
      </section>
    </main>
  );
}

async function PreCaptureNewVehicleEntryContent() {
  await connection();
  const session = await getSession();

  if (session.status === "anonymous") {
    redirect("/login");
  }

  if (session.status === "expired") {
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath="/Master-File/PreCaptureNewVehicle.aspx" />
      </main>
    );
  }

  if (session.status === "unavailable") {
    return <EntryUnavailable />;
  }

  const hasAuthorizerRole = hasVehicleInceptionAuthorizerRole(session.roles);
  const hasCapturerRole = hasVehicleInceptionCapturerRole(session.roles);

  if (hasAuthorizerRole) {
    redirect("/vehicles/authorize");
  }

  if (hasCapturerRole) {
    redirect("/vehicles/create");
  }

  redirect("/vehicles");
}

export default function PreCaptureNewVehicleEntry() {
  return (
    <Suspense fallback={<RouteLoading />}>
      <PreCaptureNewVehicleEntryContent />
    </Suspense>
  );
}

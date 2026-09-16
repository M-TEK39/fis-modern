import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";
import { Suspense } from "react";

import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import { hasAssetVerificationAccess } from "@/app/(fleet-operations)/vehicle-verification/access";
import RouteLoading from "@/components/app-shell/route-loading";
import { getSession } from "@/lib/auth/session";

function ApiUnavailable() {
  return (
    <section className="vehicle-status-card" role="alert">
      <div className="status-icon status-icon-error" aria-hidden="true">
        !
      </div>
      <p className="eyebrow">API unavailable</p>
      <h2>Asset verification is unavailable.</h2>
      <p className="muted-copy">Retry when the FIS API is available.</p>
      <Link className="button button-primary" href="/vehicle-verification">
        Try again
      </Link>
    </section>
  );
}

function AccessRestricted() {
  return (
    <section className="vehicle-status-card" role="alert">
      <p className="eyebrow">Access restricted</p>
      <h2>You do not have permission to access Vehicle Asset Verification.</h2>
    </section>
  );
}

async function AssetVerificationHelpContent() {
  await connection();
  const session = await getSession();

  if (session.status === "anonymous") redirect("/login");

  if (session.status === "expired") {
    return <SessionRecovery returnPath="/vehicle-verification/help" />;
  }

  if (session.status === "unavailable") {
    return <ApiUnavailable />;
  }

  if (!hasAssetVerificationAccess(session.roles)) {
    return <AccessRestricted />;
  }

  return (
    <div className="vehicle-status-maintenance-panel document-help-content">
      <p className="muted-copy">
        The original Asset Verification manual remains available as the read-only reference for this
        workflow.
      </p>
      <iframe
        src="/legacy/asset-verification.html"
        title="Asset Verification Help"
        className="fis-help-frame document-help-frame"
        sandbox=""
      />
    </div>
  );
}

export default function AssetVerificationHelpPage() {
  return (
    <main className="page-shell vehicle-page-shell">
      <section
        className="vehicle-card document-help-card"
        aria-labelledby="asset-verification-help-title"
      >
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Vehicle asset verification</p>
            <h1 id="asset-verification-help-title">
              Asset Verification Maintenance Information / Help
            </h1>
          </div>
          <Link className="button button-secondary" href="/vehicle-verification">
            Menu
          </Link>
        </header>
        <Suspense fallback={<RouteLoading />}>
          <AssetVerificationHelpContent />
        </Suspense>
      </section>
    </main>
  );
}

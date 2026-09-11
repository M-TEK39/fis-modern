import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";
import { Suspense } from "react";

import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import {
  AccessRestricted,
  ApiUnavailable,
} from "@/app/(fleet-operations)/full-maintenance-lease/_components";
import { hasFmlPermission } from "@/app/(fleet-operations)/full-maintenance-lease/_utils";
import { getSession } from "@/lib/auth/session";

function HelpFallback() {
  return (
    <div className="loading-card" aria-busy="true">
      <span className="spinner" aria-hidden="true" />
      <p>Checking access...</p>
    </div>
  );
}

async function FmlHelpContent() {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired")
    return <SessionRecovery returnPath="/full-maintenance-lease/help" />;
  if (session.status === "unavailable")
    return <ApiUnavailable message="FML help could not be opened." />;
  if (!hasFmlPermission(session.accessLevel)) return <AccessRestricted />;

  return (
    <>
      <div className="module-help-content">
        <section className="module-help-section" aria-labelledby="fml-help-workflow-title">
          <h2 id="fml-help-workflow-title">Lease vehicle workflow</h2>
          <p className="module-help-intro">Use the same order as the original FML menu.</p>
          <ol className="module-help-steps">
            <li>
              Capture lease vehicle tariff periods and submit contract terms for authority review.
            </li>
            <li>
              Import a tariff file when several lease tariff periods must be captured together.
            </li>
            <li>Extend the latest tariff period when the current lease period is renewed.</li>
            <li>Add a tariff period for an in-service lease vehicle that does not yet have one.</li>
            <li>
              Use the reports menu for expiry, open-contract, no-contract, maintenance, and
              kilometre-utilization checks.
            </li>
          </ol>
          <p className="module-help-note">
            The FIS API keeps the original table and field meanings. Expanded fields are used only
            when they exist; client-era fields remain the fallback.
          </p>
        </section>
      </div>
      <div className="vehicle-footer-actions">
        <Link className="button button-primary" href="/full-maintenance-lease">
          FML Menu
        </Link>
        <Link className="button button-secondary" href="/full-maintenance-lease/reports">
          FML Reports
        </Link>
      </div>
    </>
  );
}

export default function FmlHelpPage() {
  return (
    <main className="page-shell vehicle-page-shell">
      <article className="vehicle-card module-help-page" aria-labelledby="fml-page-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Full Maintenance Lease</p>
            <h1 id="fml-page-title">Full Maintenance Lease Information / Help</h1>
            <p>
              The established FML workflow for lease vehicle tariff and contract administration.
            </p>
          </div>
          <Link className="button button-secondary" href="/full-maintenance-lease">
            Back
          </Link>
        </header>
        <Suspense fallback={<HelpFallback />}>
          <FmlHelpContent />
        </Suspense>
      </article>
    </main>
  );
}

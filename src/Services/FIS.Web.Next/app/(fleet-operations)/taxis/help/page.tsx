import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";
import { Suspense } from "react";

import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import { TaxiRestricted } from "@/app/(fleet-operations)/taxis/_components";
import { getSession } from "@/lib/auth/session";
import { hasTaxiAccess } from "@/app/(fleet-operations)/taxis/access";

function HelpFallback() {
  return (
    <div className="loading-card" aria-busy="true">
      <span className="spinner" aria-hidden="true" />
      <p>Loading help…</p>
    </div>
  );
}

async function TaxiHelpContent() {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired" || session.status === "unavailable")
    return <SessionRecovery returnPath="/taxis/help" />;
  if (!hasTaxiAccess(session.roles))
    return <TaxiRestricted subject="Taxi help" />;

  return (
    <div className="module-help-content">
      <section className="module-help-section" aria-labelledby="taxi-help-manual">
        <h2 id="taxi-help-manual">Taxi maintenance manual</h2>
        <p className="module-help-intro">
          The taxi maintenance manual is available as a separate reference document.
        </p>
        <p className="module-help-note">
          Open the manual in its own window to preserve the original taxi help document and its
          navigation.
        </p>
        <div className="button-row">
          <Link
            className="button button-primary"
            href="/legacy/taxis/Doc_taxis.htm"
            target="_blank"
          >
            Open taxi help
          </Link>
        </div>
      </section>
    </div>
  );
}

export default function TaxiHelpPage() {
  return (
    <main className="page-shell vehicle-page-shell">
      <article className="vehicle-card module-help-page" aria-labelledby="taxi-help-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Taxi Maintenance</p>
            <h1 id="taxi-help-title">Taxi Maintenance Information / Help</h1>
            <p>Reference the taxi maintenance guidance.</p>
          </div>
          <Link className="button button-secondary" href="/taxis">
            Back to Taxi Menu
          </Link>
        </header>
        <Suspense fallback={<HelpFallback />}>
          <TaxiHelpContent />
        </Suspense>
      </article>
    </main>
  );
}

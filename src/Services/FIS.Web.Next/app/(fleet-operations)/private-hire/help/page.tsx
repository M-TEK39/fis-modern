import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";
import { Suspense } from "react";

import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import { getSession } from "@/lib/auth/session";

const PRIVATE_HIRE_ROLE = "Private Hire Vehicles";

function hasPrivateHireRole(roles: readonly string[]) {
  return roles.some(
    (role) => role.localeCompare(PRIVATE_HIRE_ROLE, undefined, { sensitivity: "accent" }) === 0,
  );
}

function HelpFallback() {
  return (
    <div className="loading-card" aria-busy="true">
      <span className="spinner" aria-hidden="true" />
      <p>Checking help access...</p>
    </div>
  );
}

async function PrivateHireHelpContent() {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired" || session.status === "unavailable")
    return <SessionRecovery returnPath="/private-hire/help" />;
  if (!hasPrivateHireRole(session.roles))
    return (
      <section className="vehicle-status-card" role="alert">
        <p className="eyebrow">Access restricted</p>
        <h2>You do not have permission to access Private Hire help.</h2>
      </section>
    );

  return (
    <div className="document-help-content">
      <iframe
        src="/legacy/private-hire/Doc_PrivateHire.htm"
        title="Private Hire maintenance help"
        className="help-iframe document-help-frame"
        sandbox=""
      />
    </div>
  );
}

export default function PrivateHireHelpPage() {
  return (
    <main className="page-shell vehicle-page-shell">
      <section
        className="vehicle-card document-help-card"
        aria-labelledby="private-hire-help-title"
      >
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Private Hire Vehicles</p>
            <h1 id="private-hire-help-title">Private Hire Information / Help</h1>
            <p>Reference the original Private Hire maintenance guidance.</p>
          </div>
          <Link className="button button-secondary" href="/private-hire">
            Back
          </Link>
        </header>
        <Suspense fallback={<HelpFallback />}>
          <PrivateHireHelpContent />
        </Suspense>
      </section>
    </main>
  );
}

import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import {
  hasContractAccess,
  hasContractBackdatingApproverRole,
  hasContractHistoryBackdatingRole,
} from "@/app/(fleet-operations)/contracts/access";
import { MenuSection } from "@/components/ui/menu-section";
import { StreamedRoute } from "@/components/app-shell/streamed-route";
import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import { getSession } from "@/lib/auth/session";

function AccessRestricted() {
  return (
    <section className="vehicle-status-card" role="alert">
      <p className="eyebrow">Access restricted</p>
      <h2>You do not have permission to access Contracts.</h2>
      <p className="muted-copy">Your account needs the legacy Contract Management permission.</p>
    </section>
  );
}

async function ContractsPageContent() {
  await connection();
  const session = await getSession();

  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired") {
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath="/contracts" />
      </main>
    );
  }
  if (session.status === "unavailable") {
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <p className="eyebrow">API unavailable</p>
          <h2>Contracts could not be opened.</h2>
          <p className="muted-copy">Retry when the FIS API is available.</p>
        </section>
      </main>
    );
  }
  if (!hasContractAccess(session.accessLevel, session.roles)) {
    return (
      <main className="page-shell vehicle-page-shell">
        <AccessRestricted />
      </main>
    );
  }

  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-labelledby="contracts-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Contracts</p>
            <h1 id="contracts-title">Contracts Menu</h1>
            <p>Maintain vehicle contracts using the established FIS workflow.</p>
          </div>
          <Link className="button button-secondary" href="/home">
            Home
          </Link>
        </header>

        <div className="vehicle-menu-tiles">
          <MenuSection title="Contracts Information / Help">
            <Link className="vehicle-menu-link" href="/contracts/help">
              Contracts Information / Help
            </Link>
          </MenuSection>
          <MenuSection title="Contract Maintenance">
            <Link className="vehicle-menu-link" href="/contracts/maintenance">
              1) Vehicle Contract Maintenance
            </Link>
            {hasContractBackdatingApproverRole(session.roles) ? (
              <Link className="vehicle-menu-link" href="/contracts/backdating-approval">
                2) Vehicle Contract Back Date Requests - Approval
              </Link>
            ) : null}
            {hasContractHistoryBackdatingRole(session.roles) ? (
              <Link className="vehicle-menu-link" href="/contracts/backdating-history">
                3) Vehicle Contract History Backdating
              </Link>
            ) : null}
            <Link className="vehicle-menu-link" href="/contracts/print-menu">
              4) Contract Print Out Menu
            </Link>
          </MenuSection>
        </div>
      </section>
    </main>
  );
}

export default function ContractsPage() {
  return (
    <StreamedRoute>
      <ContractsPageContent />
    </StreamedRoute>
  );
}

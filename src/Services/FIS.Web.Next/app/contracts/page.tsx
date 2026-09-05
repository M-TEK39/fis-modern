import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import SessionRecovery from "@/app/home/session-recovery";
import { getSession } from "@/lib/session";

const CONTRACT_PERMISSION = BigInt(2);

function hasContractAccess(accessLevel: string | undefined, roles: readonly string[]) {
  if (roles.some((role) => ["contracts", "contract", "admin", "administrator"].includes(role.trim().toLowerCase()))) {
    return true;
  }

  try {
    return accessLevel ? (BigInt(accessLevel) & CONTRACT_PERMISSION) === CONTRACT_PERMISSION : false;
  } catch {
    return false;
  }
}

function hasApproverRole(roles: readonly string[]) {
  return roles.some((role) => ["contracts approver", "contracts_approver", "back dating contract (approver)", "admin", "administrator"].includes(role.trim().toLowerCase()));
}

function hasHistoryRole(roles: readonly string[]) {
  return roles.some((role) => ["contract history back dating", "contract_history_backdating", "admin", "administrator"].includes(role.trim().toLowerCase()));
}

function AccessRestricted() {
  return (
    <section className="vehicle-status-card" role="alert">
      <p className="eyebrow">Access restricted</p>
      <h2>You do not have permission to access Contracts.</h2>
      <p className="muted-copy">Your account needs the legacy Contract Management permission.</p>
    </section>
  );
}

export default async function ContractsPage() {
  await connection();
  const session = await getSession();

  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired") {
    return <main className="page-shell vehicle-page-shell"><SessionRecovery returnPath="/contracts" /></main>;
  }
  if (session.status === "unavailable") {
    return <main className="page-shell vehicle-page-shell"><section className="vehicle-status-card" role="alert"><p className="eyebrow">API unavailable</p><h2>Contracts could not be opened.</h2><p className="muted-copy">Retry when the FIS API is available.</p></section></main>;
  }
  if (!hasContractAccess(session.accessLevel, session.roles)) {
    return <main className="page-shell vehicle-page-shell"><AccessRestricted /></main>;
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
          <Link className="button button-secondary" href="/home">Home</Link>
        </header>

        <div className="vehicle-menu-tiles">
          <section className="vehicle-menu-tile">
            <h2 className="vehicle-menu-header">Contracts Information / Help</h2>
            <div className="vehicle-menu-body"><Link className="vehicle-menu-link" href="/contracts/help">Contracts Information / Help</Link></div>
          </section>
          <section className="vehicle-menu-tile">
            <h2 className="vehicle-menu-header">Contract Maintenance</h2>
            <div className="vehicle-menu-body">
              <Link className="vehicle-menu-link" href="/contracts/maintenance">1) Vehicle Contract Maintenance</Link>
              {hasApproverRole(session.roles) ? <Link className="vehicle-menu-link" href="/contracts/backdating-approval">2) Vehicle Contract Back Date Requests - Approval</Link> : null}
              {hasHistoryRole(session.roles) ? <Link className="vehicle-menu-link" href="/contracts/backdating-history">3) Vehicle Contract History Backdating</Link> : null}
              <Link className="vehicle-menu-link" href="/contracts/print-menu">4) Contract Print Out Menu</Link>
            </div>
          </section>
        </div>
      </section>
    </main>
  );
}

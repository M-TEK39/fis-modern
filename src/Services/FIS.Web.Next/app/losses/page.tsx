import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import SessionRecovery from "@/app/home/session-recovery";
import { LossApiError, getLosses } from "@/lib/api-losses";
import { getSession } from "@/lib/session";

const LOSS_ROLE = "Losses";

function hasLossRole(roles: readonly string[]) {
  return roles.some((role) => role.localeCompare(LOSS_ROLE, undefined, { sensitivity: "accent" }) === 0);
}

function valueOrDash(value: string | number | null | undefined) {
  return value === null || value === undefined || String(value).trim() === "" ? "-" : String(value);
}

function statusFor(loss: { lossStatus: string | null; cancelled: boolean }) {
  return loss.cancelled ? "Cancelled" : loss.lossStatus || "Open";
}

function ApiUnavailable() {
  return (
    <section className="vehicle-status-card" role="alert">
      <p className="eyebrow">API unavailable</p>
      <h2>Losses could not be loaded.</h2>
      <p className="muted-copy">The application is still running. Retry when the FIS API is available.</p>
      <Link className="button button-primary" href="/losses">Try again</Link>
    </section>
  );
}

export default async function LossesPage() {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired") return <main className="page-shell vehicle-page-shell"><SessionRecovery returnPath="/losses" /></main>;
  if (session.status === "unavailable") return <main className="page-shell vehicle-page-shell"><ApiUnavailable /></main>;
  if (!hasLossRole(session.roles)) {
    return <main className="page-shell vehicle-page-shell"><section className="vehicle-status-card" role="alert"><p className="eyebrow">Access restricted</p><h2>You do not have permission to access Losses.</h2><p className="muted-copy">This menu requires the Losses role.</p></section></main>;
  }

  let losses;
  try {
    losses = await getLosses();
  } catch (error) {
    if (error instanceof LossApiError && error.reason === "unauthorized") return <main className="page-shell vehicle-page-shell"><SessionRecovery returnPath="/losses" /></main>;
    console.error("FIS losses menu request failed", error instanceof Error ? error.message : "unknown error");
    return <main className="page-shell vehicle-page-shell"><ApiUnavailable /></main>;
  }

  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-labelledby="losses-title">
        <header className="vehicle-page-header">
          <div><p className="eyebrow">Losses</p><h1 id="losses-title">Losses Maintenance Menu</h1><p>Review and maintain the vehicle loss records used by the legacy fleet process.</p></div>
          <Link className="button button-secondary" href="/home">Home</Link>
        </header>

        <div className="vehicle-menu-tiles">
          <section className="vehicle-menu-tile">
            <h2 className="vehicle-menu-header">Losses Maintenance Menu</h2>
            <div className="vehicle-menu-body"><Link className="vehicle-menu-link" href="/Losses/Doc/Doc_losses.htm">Losses Maintenance Information / Help</Link></div>
          </section>
          <section className="vehicle-menu-tile">
            <h2 className="vehicle-menu-header">Losses Maintenance</h2>
            <div className="vehicle-menu-body"><Link className="vehicle-menu-link" href="/Losses/MNT_Loss_GetGg.aspx">1) Vehicle Losses Maintenance</Link></div>
          </section>
        </div>

        <section className="vehicle-status-maintenance-panel" aria-labelledby="loss-preview-title">
          <div className="vehicle-form-section-header"><div><p className="eyebrow">Current records</p><h2 id="loss-preview-title">Vehicle Loss Preview</h2></div></div>
          {losses.length === 0 ? <p className="muted-copy">No active loss records found.</p> : (
            <div className="vehicle-table-wrapper">
              <table className="vehicle-table">
                <caption className="sr-only">Vehicle loss records</caption>
                <thead><tr><th scope="col">Loss Date</th><th scope="col">Reference</th><th scope="col">Vehicle</th><th scope="col">Type</th><th scope="col">Status</th><th scope="col">Amount</th><th scope="col">Actions</th></tr></thead>
                <tbody>{losses.map((loss) => <tr key={loss.lossCode}><td>{valueOrDash(loss.lossDate?.slice(0, 10))}</td><td>{valueOrDash(loss.lossReference)}</td><td>{valueOrDash(loss.vehicleIdentifier ?? loss.vmfCode)}</td><td>{valueOrDash(loss.lossTypeDescription ?? loss.lossTypeCode)}</td><td>{statusFor(loss)}</td><td>{loss.lossAmount === null ? "-" : loss.lossAmount.toFixed(2)}</td><td><div className="button-row"><Link className="button button-secondary button-small" href={`/Losses/MNT_Losses_Edit.aspx?loss_code=${loss.lossCode}`}>Edit</Link><Link className="button button-danger button-small" href={`/Losses/MNT_Losses_Delete.aspx?loss_code=${loss.lossCode}`}>Delete</Link></div></td></tr>)}</tbody>
              </table>
            </div>
          )}
        </section>
      </section>
    </main>
  );
}

import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import SessionRecovery from "@/app/home/session-recovery";
import { getSession } from "@/lib/session";

export default async function ContractsHelpPage() {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired") return <main className="page-shell vehicle-page-shell"><SessionRecovery returnPath="/contracts/help" /></main>;
  if (session.status !== "authenticated") return <main className="page-shell vehicle-page-shell"><section className="vehicle-status-card" role="alert"><p className="eyebrow">API unavailable</p><h2>Contracts help could not be opened.</h2><p className="muted-copy">Retry when the FIS API is available.</p></section></main>;

  return <main className="page-shell vehicle-page-shell"><section className="vehicle-card" aria-labelledby="contracts-help-title"><header className="vehicle-page-header"><div><p className="eyebrow">Contracts information</p><h1 id="contracts-help-title">Contracts Information / Help</h1><p>The contract module preserves the established vehicle hire, approval, activation, extension, reassignment, relief, close, and return-home workflow.</p></div><Link className="button button-secondary" href="/contracts">Contracts Menu</Link></header><section className="vehicle-status-maintenance-panel"><h2>Workflow summary</h2><ol className="form-hint fis-list-pad"><li>Search for a vehicle by its GG number or registration number.</li><li>Open a new contract or select an existing contract record.</li><li>Submit draft contracts for review when approval is required.</li><li>Reviewers approve, activate, return for correction, or decline pending contracts.</li><li>Active contracts can be extended, reassigned within the established site workflow, or closed and returned to a home GFleet site.</li><li>Eligible active contracts can open the relief-vehicle search and create a linked relief contract.</li></ol><p className="muted-copy">Legacy contract fields remain available on the detail screen, including driver, authorisation, BAS, billing, relief, and capture/audit values when the connected database provides them.</p></section><div className="vehicle-footer-actions"><Link className="button button-secondary" href="/contracts/maintenance">Vehicle Contract Maintenance</Link><Link className="button button-secondary" href="/home">Home</Link></div></section></main>;
}

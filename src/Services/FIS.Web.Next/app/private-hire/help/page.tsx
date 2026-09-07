import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import SessionRecovery from "@/app/home/session-recovery";
import { getSession } from "@/lib/session";

export default async function PrivateHireHelpPage() {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired" || session.status === "unavailable") return <main className="page-shell vehicle-page-shell"><SessionRecovery returnPath="/private-hire/help" /></main>;
  if (!session.roles.some((role) => role.localeCompare("Private Hire Vehicles", undefined, { sensitivity: "accent" }) === 0)) return <main className="page-shell vehicle-page-shell"><section className="vehicle-status-card" role="alert"><p className="eyebrow">Access restricted</p><h2>You do not have permission to access Private Hire help.</h2></section></main>;
  return <main className="page-shell vehicle-page-shell"><section className="vehicle-card" aria-labelledby="private-hire-help-title"><header className="vehicle-page-header"><div><p className="eyebrow">Private Hire Vehicles</p><h1 id="private-hire-help-title">Private Hire Information / Help</h1><p>Reference the original Private Hire maintenance guidance.</p></div><Link className="button button-secondary" href="/private-hire">Back</Link></header><iframe src="/legacy/private-hire/Doc_PrivateHire.htm" title="Private Hire maintenance help" className="help-iframe" /></section></main>;
}

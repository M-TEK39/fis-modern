import { connection } from "next/server";
import { redirect } from "next/navigation";

import SessionRecovery from "@/app/home/session-recovery";
import { getSession } from "@/lib/session";
import { getTroubleshootUsers, TroubleshootApiError } from "@/lib/api-troubleshoot";
import { hasTroubleshootingRole, StatusCard, TroubleshootMenu, TroubleshootShell, valueOrDash } from "@/app/troubleshoot/_components";

export type TroubleshootPageProps = {
  searchParams?: Promise<Record<string, string | string[] | undefined>>;
  routePath?: string;
};

export default async function TroubleshootPage({ routePath = "/troubleshoot" }: TroubleshootPageProps) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired") return <main className="page-shell vehicle-page-shell"><SessionRecovery returnPath={routePath} /></main>;
  if (session.status === "unavailable") return <main className="page-shell vehicle-page-shell"><StatusCard title="API unavailable" message="The sign-in service is temporarily unavailable." href={routePath} /></main>;
  if (!hasTroubleshootingRole(session.roles)) return <main className="page-shell vehicle-page-shell"><StatusCard title="Access restricted" message="You do not have permission to access Troubleshoot." href="/home" /></main>;

  try {
    const users = await getTroubleshootUsers();
    return <TroubleshootShell title="Troubleshoot Maintenance Menu" description="Maintain troubleshooting records while preserving the original FIS workflow."><TroubleshootMenu /><section className="vehicle-status-maintenance-panel" aria-labelledby="troubleshoot-preview-title"><div className="vehicle-form-section-header"><div><p className="eyebrow">Troubleshoot user preview</p><h2 id="troubleshoot-preview-title">Active users</h2></div><span className="form-hint">{users.length} user{users.length === 1 ? "" : "s"}</span></div>{users.length === 0 ? <p className="muted-copy">No active troubleshoot users were found.</p> : <div className="vehicle-table-wrapper"><table className="vehicle-table"><caption className="sr-only">Active troubleshoot users</caption><thead><tr><th scope="col">Access Code</th><th scope="col">User</th><th scope="col">Site</th><th scope="col">Status</th></tr></thead><tbody>{users.slice(0, 100).map((user) => <tr key={user.userAccessCode}><td>{user.userAccessCode}</td><td>{valueOrDash(user.name)}</td><td>{valueOrDash(user.siteDescription)}</td><td>{user.siteDescription ? "Site Linked" : "No Site"}</td></tr>)}</tbody></table></div>}</section></TroubleshootShell>;
  } catch (error) {
    console.error("FIS troubleshoot user preview failed", error instanceof TroubleshootApiError ? error.message : "unknown error");
    return <TroubleshootShell title="Troubleshoot Maintenance Menu" description="Maintain troubleshooting records while preserving the original FIS workflow."><TroubleshootMenu /><StatusCard title="API unavailable" message="Troubleshoot users could not be loaded." href={routePath} /></TroubleshootShell>;
  }
}

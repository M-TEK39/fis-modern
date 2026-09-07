import Link from "next/link";

import { LogsheetShell } from "@/app/log-sheets/_components";
import { accessRestricted, getLogsheetSession, hasLogsheetAccess, sessionMessage } from "@/app/log-sheets/_page";

export default async function LogsheetHelpPage() {
  const session = await getLogsheetSession();
  const problem = sessionMessage(session, "/log-sheets/help");
  if (problem) return problem;
  if (session.status !== "authenticated") return accessRestricted("Your session could not be loaded.");
  if (!hasLogsheetAccess(session)) return accessRestricted("Your profile does not include Reports access.");

  return <LogsheetShell title="Logsheet Maintenance Information / Help" description="Use this workflow to capture and report monthly vehicle usage."><section className="vehicle-status-maintenance-panel"><h2>Logsheet workflow</h2><ol><li>Search for a vehicle by its GG or GP number before entering a logsheet.</li><li>Enter the requisition, month, odometer readings, days used, batch number, and site.</li><li>Use the reports to review captured activity or total kilometres by vehicle class.</li><li>Editing and deletion remain restricted to the legacy logsheet manager access codes.</li></ol><p className="muted-copy">The service reads the original required `dbo.Logsheets` columns and uses expanded audit columns when the database provides them.</p><div className="button-row"><Link className="button button-primary" href="/log-sheets/enter">Enter a Logsheet</Link><Link className="button button-secondary" href="/log-sheets">Back to menu</Link></div></section></LogsheetShell>;
}

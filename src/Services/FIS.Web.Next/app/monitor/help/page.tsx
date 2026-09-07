import Link from "next/link";

import { MonitorShell } from "@/app/monitor/_components";

export default function MonitorHelpPage() {
  return <MonitorShell title="Monitor Inquiry Information / Help" description="Reference information for the Monitor inquiry workflow."><section className="vehicle-status-maintenance-panel"><h2>Monitor inquiry workflow</h2><p className="muted-copy">Capture an inquiry against the selected vehicle, keep the driver and site details current, and use Reports to review captured inquiries.</p><p className="muted-copy">The original Call Centre role and legacy Monitor fields remain in force. If the API is temporarily unavailable, retry after the service has recovered.</p><Link className="button button-secondary" href="/monitor">Monitor menu</Link></section></MonitorShell>;
}

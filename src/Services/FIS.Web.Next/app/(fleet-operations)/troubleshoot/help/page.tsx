import Link from "next/link";

import { TroubleshootShell } from "@/app/(fleet-operations)/troubleshoot/_components";

export default function TroubleshootHelpPage() {
  return (
    <TroubleshootShell
      title="Troubleshoot Maintenance Information / Help"
      description="Troubleshoot workflow guidance and references."
    >
      <section className="vehicle-status-maintenance-panel">
        <h2>Troubleshoot workflow</h2>
        <p className="muted-copy">
          Use Troubleshoot Log to review entries for a department/site, General Reports to filter
          logged problems, and ODOMeter Corrections to locate the current and last recorded odometer
          values.
        </p>
        <p className="muted-copy">
          Vehicle Master Edit searches the existing vehicle master record. Recovered vehicles
          continue through the original recovered-GG transaction so the stolen row, replacement row,
          and history remain consistent.
        </p>
        <Link className="button button-secondary" href="/troubleshoot">
          Troubleshoot menu
        </Link>
      </section>
    </TroubleshootShell>
  );
}

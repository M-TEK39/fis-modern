import Link from "next/link";

import {
  LogsheetMenu,
  LogsheetShell,
  LogsheetTable,
} from "@/app/(fleet-operations)/log-sheets/_components";
import {
  accessRestricted,
  getLogsheetSession,
  hasLogsheetAccess,
  hasLogsheetManagerAccess,
  sessionMessage,
} from "@/app/(fleet-operations)/log-sheets/_page";
import { getLogsheets, LogsheetApiError } from "@/lib/api/fleet-operations/api-logsheets";

export default async function LogsheetMenuPage() {
  const session = await getLogsheetSession();
  const problem = sessionMessage(session, "/log-sheets");
  if (problem) return problem;
  if (session.status !== "authenticated")
    return accessRestricted("Your session could not be loaded.");
  if (!hasLogsheetAccess(session))
    return accessRestricted("Your profile does not include Reports access.");

  try {
    const records = (await getLogsheets()).slice(0, 12);
    return (
      <LogsheetShell
        title="Logsheet Maintenance"
        description="Capture and manage monthly vehicle usage logs."
      >
        <LogsheetMenu canManage={hasLogsheetManagerAccess(session)} />
        <div className="button-row">
          <Link className="button button-secondary" href="/manuals">
            User manuals
          </Link>
        </div>
        <section
          className="vehicle-status-maintenance-panel"
          aria-labelledby="recent-logsheets-title"
        >
          <div className="vehicle-form-section-header">
            <div>
              <p className="eyebrow">Recent activity</p>
              <h2 id="recent-logsheets-title">Latest logsheets</h2>
            </div>
          </div>
          <LogsheetTable records={records} mode="preview" returnPath="/log-sheets" />
        </section>
      </LogsheetShell>
    );
  } catch (error) {
    return (
      <LogsheetShell
        title="Logsheet Maintenance"
        description="Capture and manage monthly vehicle usage logs."
      >
        <LogsheetMenu canManage={hasLogsheetManagerAccess(session)} />
        <section className="vehicle-status-card" role="alert">
          <h2>
            {error instanceof LogsheetApiError
              ? "The Logsheet service is temporarily unavailable."
              : "Logsheets could not be loaded."}
          </h2>
          <p className="muted-copy">
            The maintenance menu is available while the service is restored.
          </p>
        </section>
      </LogsheetShell>
    );
  }
}

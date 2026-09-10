import Link from "next/link";

import { LogbookShell } from "@/app/(fleet-operations)/log-books/_components";
import {
  accessRestricted,
  getLogbookSession,
  hasLogbookAccess,
  sessionMessage,
} from "@/app/(fleet-operations)/log-books/_page";

export default async function LogbookHelpPage() {
  const session = await getLogbookSession();
  const problem = sessionMessage(session, "/log-books/help");
  if (problem) return problem;
  if (session.status !== "authenticated")
    return accessRestricted("Your session could not be loaded.");
  if (!hasLogbookAccess(session))
    return accessRestricted("Your profile does not include Logbooks access.");
  return (
    <LogbookShell
      title="Logbook Maintenance Information / Help"
      description="Logbook guidance and operational notes."
    >
      <section className="vehicle-status-maintenance-panel">
        <p>
          Use Maintenance to review or add a handout for one vehicle. Use Collection when several
          vehicles receive logbooks together. Use Delete only when a handout has been returned and
          should be removed from the active list.
        </p>
        <div className="button-row">
          <Link className="button button-primary" href="/log-books/maintenance">
            Maintenance
          </Link>
          <Link className="button button-secondary" href="/log-books">
            Main menu
          </Link>
        </div>
      </section>
    </LogbookShell>
  );
}

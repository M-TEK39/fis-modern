import Link from "next/link";

import { LicenseShell } from "@/app/(fleet-operations)/licenses/_components";
import {
  accessRestricted,
  getLicenseSession,
  hasLicenseAccess,
  sessionMessage,
} from "@/app/(fleet-operations)/licenses/_page";

export default async function LicenseHelpPage() {
  const session = await getLicenseSession();
  const problem = sessionMessage(session, "/licenses/help");
  if (problem) return problem;
  if (session.status !== "authenticated")
    return accessRestricted("Your session could not be loaded.");
  if (!hasLicenseAccess(session)) return accessRestricted();

  return (
    <LicenseShell
      title="Licence Maintenance Help"
      description="Guidance for licence maintenance and certificate workflows."
    >
      <section className="vehicle-status-maintenance-panel" aria-labelledby="license-help-title">
        <div className="vehicle-form-section-header">
          <div>
            <p className="eyebrow">Licence maintenance</p>
            <h2 id="license-help-title">Workflow guidance</h2>
          </div>
        </div>
        <ol>
          <li>
            Maintain the newest licence expiry date and registration document for each vehicle.
          </li>
          <li>
            Use the one-vehicle workflow for a single update, or the multi-collection workflow when
            collecting two or more licences.
          </li>
          <li>
            Use the garage workflow when a licence is available for collection at Johannesburg or
            Pretoria.
          </li>
          <li>
            Keep the receiver name, identification, telephone, site, collection date, and COF
            information accurate for audit and reporting.
          </li>
        </ol>
        <p className="muted-copy">
          The licence reports use the same vehicle and licence data as the maintenance workflows. If
          a report returns no rows, check the vehicle identifiers and date range.
        </p>
      </section>
      <div className="button-row">
        <Link className="button button-primary" href="/manuals">
          Open Manuals
        </Link>
        <Link className="button button-secondary" href="/licenses">
          Back to Licence Menu
        </Link>
      </div>
    </LicenseShell>
  );
}

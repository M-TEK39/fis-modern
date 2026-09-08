import Link from "next/link";

import { LicenseMenu, LicenseNotice, LicenseShell, valueOrDash } from "@/app/licenses/_components";
import {
  accessRestricted,
  getLicenseSession,
  hasLicenseAccess,
  queryValue,
  sessionMessage,
} from "@/app/licenses/_page";
import { getLicenseTypes, ReferenceDataApiError } from "@/lib/api-reference-data";

export default async function LicensesPage({
  searchParams,
}: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  const session = await getLicenseSession();
  const problem = sessionMessage(session, "/licenses");
  if (problem) return problem;
  if (session.status !== "authenticated")
    return accessRestricted("Your session could not be loaded.");
  if (!hasLicenseAccess(session))
    return accessRestricted("Legacy parity: this menu requires the Licence role.");
  const query = await searchParams;
  let types: Awaited<ReturnType<typeof getLicenseTypes>> = [];
  let unavailable = false;
  try {
    types = await getLicenseTypes();
  } catch (error) {
    unavailable = error instanceof ReferenceDataApiError && error.reason !== "unauthorized";
  }

  return (
    <LicenseShell title="Licence Maintenance Menu" description="Licence maintenance workflows.">
      <LicenseNotice query={query} />
      <LicenseMenu />
      <section className="vehicle-status-maintenance-panel" aria-labelledby="license-preview-title">
        <div className="vehicle-form-section-header">
          <div>
            <p className="eyebrow">{types.length} licence types</p>
            <h2 id="license-preview-title">Licence Type Preview</h2>
          </div>
        </div>
        {unavailable ? (
          <p className="muted-copy">
            Licence types could not be loaded. The maintenance menu remains available; retry when
            the API is available.
          </p>
        ) : types.length === 0 ? (
          <p className="muted-copy">No licence types found.</p>
        ) : (
          <div className="vehicle-table-wrapper">
            <table className="vehicle-table">
              <caption className="sr-only">Licence type preview</caption>
              <thead>
                <tr>
                  <th scope="col">Code</th>
                  <th scope="col">Description</th>
                  <th scope="col">Category</th>
                </tr>
              </thead>
              <tbody>
                {types.slice(0, 100).map((type) => (
                  <tr key={type.licenceCode}>
                    <td>{type.licenceCode}</td>
                    <td>{valueOrDash(type.description)}</td>
                    <td>{valueOrDash(type.category)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>
      <div className="vehicle-footer-actions">
        <Link className="button button-secondary" href="/home">
          Home
        </Link>
      </div>
    </LicenseShell>
  );
}

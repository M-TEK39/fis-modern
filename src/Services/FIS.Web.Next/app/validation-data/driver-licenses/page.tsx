import Link from "next/link";
import { connection } from "next/server";
import { redirect } from "next/navigation";

import { logoutAction } from "@/app/actions/auth";
import { hasVehicleManagementPermission } from "@/app/drivers/access";
import SessionRecovery from "@/app/home/session-recovery";
import {
  DriverLicenceApiError,
  getDriverLicences,
  type DriverLicenceRecord,
} from "@/lib/api-driver-licences";
import { getSession } from "@/lib/session";

export type DriverLicenceListPageProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
  routePath?: string;
};

function getQueryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

function valueOrDash(value: string | number | null | undefined) {
  return value === null || value === undefined || String(value).trim() === "" ? "-" : String(value);
}

function editPath(licenceCode: number) {
  return `/Validation/MNT_DriversLicence_Edit.aspx?cmbDriverL=${encodeURIComponent(String(licenceCode))}`;
}

function deleteCheckPath(licenceCode: number) {
  return `/Validation/MNT_DriversLicence_Del_Check.aspx?code=${encodeURIComponent(String(licenceCode))}`;
}

function ErrorCard({ message }: Readonly<{ message: string }>) {
  return (
    <section className="vehicle-status-card" role="alert">
      <p className="eyebrow">Driver licence maintenance</p>
      <h2>{message}</h2>
      <Link className="button button-secondary" href="/validation-data">
        Validation Data
      </Link>
    </section>
  );
}

function ApiUnavailable({ routePath }: Readonly<{ routePath: string }>) {
  return (
    <section className="vehicle-status-card" role="alert">
      <p className="eyebrow">API unavailable</p>
      <h2>Driver licences could not be loaded.</h2>
      <p className="muted-copy">
        The application is still running. Retry when the FIS API is available.
      </p>
      <div className="button-row">
        <Link className="button button-primary" href={routePath}>
          Try again
        </Link>
        <Link className="button button-secondary" href="/validation-data">
          Validation Data
        </Link>
      </div>
    </section>
  );
}

function DriverLicenceTable({ licences }: Readonly<{ licences: DriverLicenceRecord[] }>) {
  if (licences.length === 0)
    return (
      <div className="empty-state">
        <h2>No driver licence types found</h2>
        <p>Add a driver licence type using the same legacy validation workflow.</p>
        <Link className="button button-primary" href="/Validation/MNT_Driverslicence_Add.aspx">
          Add Driver Licence
        </Link>
      </div>
    );

  return (
    <div className="table-container">
      <div className="table-header">
        <span className="table-title">
          {licences.length} driver licence{licences.length === 1 ? "" : "s"}
        </span>
      </div>
      <div className="table-wrapper">
        <table className="data-table">
          <caption className="sr-only">Legacy driver licence types</caption>
          <thead>
            <tr>
              <th scope="col">Licence code</th>
              <th scope="col">Description</th>
              <th scope="col">Last updated</th>
              <th scope="col">Actions</th>
            </tr>
          </thead>
          <tbody>
            {licences.map((licence) => (
              <tr key={licence.licenceCode}>
                <td>{licence.licenceCode}</td>
                <td>{valueOrDash(licence.description)}</td>
                <td>{valueOrDash(licence.dateUpdated?.slice(0, 10))}</td>
                <td className="actions-column">
                  <div className="table-actions">
                    <Link
                      aria-label={`Edit ${licence.description || `driver licence ${licence.licenceCode}`}`}
                      className="button button-secondary button-small"
                      href={editPath(licence.licenceCode)}
                    >
                      Edit
                    </Link>
                    <Link
                      aria-label={`Delete ${licence.description || `driver licence ${licence.licenceCode}`}`}
                      className="button button-secondary button-small"
                      href={deleteCheckPath(licence.licenceCode)}
                    >
                      Delete
                    </Link>
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}

export default async function DriverLicenceListPage({
  searchParams,
  routePath = "/validation-data/driver-licenses",
}: DriverLicenceListPageProps) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired")
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath={routePath} />
      </main>
    );
  if (session.status === "unavailable")
    return (
      <main className="page-shell vehicle-page-shell">
        <ApiUnavailable routePath={routePath} />
      </main>
    );
  if (!hasVehicleManagementPermission(session.accessLevel))
    return (
      <main className="page-shell vehicle-page-shell">
        <ErrorCard message="You do not have permission to maintain driver licences." />
      </main>
    );

  const query = await searchParams;
  const saved = getQueryValue(query.saved);
  const error = getQueryValue(query.error);
  try {
    const licences = await getDriverLicences();
    const notice =
      saved === "created"
        ? "Driver licence added successfully."
        : saved === "updated"
          ? "Driver licence updated successfully."
          : saved === "deleted"
            ? "Driver licence deleted successfully."
            : error;
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-card" aria-labelledby="driver-licence-list-title">
          <header className="vehicle-page-header">
            <div>
              <p className="eyebrow">Validation / Licence</p>
              <h1 id="driver-licence-list-title">Drivers Licence Maintenance</h1>
              <p>Maintain the legacy driver licence descriptions used by vehicle models.</p>
            </div>
            <div className="button-row">
              <Link
                className="button button-primary"
                href="/Validation/MNT_Driverslicence_Add.aspx"
              >
                Add Driver Licence
              </Link>
              <Link className="button button-secondary" href="/validation-data">
                Validation Data
              </Link>
            </div>
          </header>
          {notice ? (
            <div
              className={`notice ${error ? "notice-error" : "notice-success"}`}
              role={error ? "alert" : "status"}
            >
              {notice}
            </div>
          ) : null}
          <DriverLicenceTable licences={licences} />
          <div className="vehicle-footer-actions">
            <Link className="button button-secondary" href="/home">
              Home
            </Link>
            <form action={logoutAction}>
              <button className="button button-secondary" type="submit">
                Sign out
              </button>
            </form>
          </div>
        </section>
      </main>
    );
  } catch (caughtError) {
    if (caughtError instanceof DriverLicenceApiError && caughtError.reason === "unauthorized")
      return (
        <main className="page-shell vehicle-page-shell">
          <SessionRecovery returnPath={routePath} />
        </main>
      );
    console.error(
      "FIS driver licence list request failed",
      caughtError instanceof Error ? caughtError.message : "unknown error",
    );
    return (
      <main className="page-shell vehicle-page-shell">
        <ApiUnavailable routePath={routePath} />
      </main>
    );
  }
}

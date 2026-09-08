import Link from "next/link";
import { connection } from "next/server";
import { redirect } from "next/navigation";

import { logoutAction } from "@/app/actions/auth";
import { hasVehicleManagementPermission } from "@/app/drivers/access";
import SessionRecovery from "@/app/home/session-recovery";
import { MakeApiError, getMakes, type MakeRecord } from "@/lib/api-makes";
import { getSession } from "@/lib/session";

export type MakeListPageProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
  routePath?: string;
};

function getQueryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

function valueOrDash(value: string | number | null | undefined) {
  return value === null || value === undefined || String(value).trim() === "" ? "-" : String(value);
}

function editPath(makeCode: number) {
  return `/Validation/MNT_Make_Edit.aspx?cmbMake=${encodeURIComponent(String(makeCode))}`;
}

function deleteCheckPath(makeCode: number) {
  return `/Validation/MNT_Make_Del_Check.aspx?code=${encodeURIComponent(String(makeCode))}`;
}

function AccessRestricted() {
  return (
    <section className="vehicle-status-card" role="alert">
      <p className="eyebrow">Access restricted</p>
      <h2>You do not have permission to maintain vehicle makes.</h2>
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
      <h2>Vehicle makes could not be loaded.</h2>
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

function MakeTable({ makes }: Readonly<{ makes: MakeRecord[] }>) {
  if (makes.length === 0)
    return (
      <div className="empty-state">
        <h2>No makes found</h2>
        <p>Add a make using the legacy vehicle-validation workflow.</p>
        <Link className="button button-primary" href="/Validation/MNT_Make_Add.aspx">
          Add Make
        </Link>
      </div>
    );
  return (
    <div className="table-container">
      <div className="table-header">
        <span className="table-title">
          {makes.length} make{makes.length === 1 ? "" : "s"}
        </span>
      </div>
      <div className="table-wrapper">
        <table className="data-table">
          <caption className="sr-only">Legacy vehicle makes</caption>
          <thead>
            <tr>
              <th scope="col">Make code</th>
              <th scope="col">Make name</th>
              <th scope="col">Last updated</th>
              <th scope="col">Actions</th>
            </tr>
          </thead>
          <tbody>
            {makes.map((make) => (
              <tr key={make.makeCode}>
                <td>{make.makeCode}</td>
                <td>{valueOrDash(make.makeDescription)}</td>
                <td>{valueOrDash(make.dateUpdated?.slice(0, 10))}</td>
                <td className="actions-column">
                  <div className="table-actions">
                    <Link
                      className="button button-secondary button-small"
                      href={editPath(make.makeCode)}
                    >
                      Edit
                    </Link>
                    <Link
                      className="button button-secondary button-small"
                      href={deleteCheckPath(make.makeCode)}
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

export default async function MakeListPage({
  searchParams,
  routePath = "/validation-data/makes",
}: MakeListPageProps) {
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
        <AccessRestricted />
      </main>
    );

  const query = await searchParams;
  const saved = getQueryValue(query.saved);
  const error = getQueryValue(query.error);
  try {
    const makes = await getMakes();
    const notice =
      saved === "created"
        ? "Make added successfully."
        : saved === "updated"
          ? "Make updated successfully."
          : saved === "deleted"
            ? "Make deleted successfully."
            : error;
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-card" aria-labelledby="make-list-title">
          <header className="vehicle-page-header">
            <div>
              <p className="eyebrow">Validation / Vehicle</p>
              <h1 id="make-list-title">Make Maintenance</h1>
              <p>Maintain the legacy vehicle make list used by model and vehicle workflows.</p>
            </div>
            <div className="button-row">
              <Link className="button button-primary" href="/Validation/MNT_Make_Add.aspx">
                Add Make
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
          <MakeTable makes={makes} />
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
  } catch (error) {
    if (error instanceof MakeApiError && error.reason === "unauthorized")
      return (
        <main className="page-shell vehicle-page-shell">
          <SessionRecovery returnPath={routePath} />
        </main>
      );
    console.error(
      "FIS make list request failed",
      error instanceof Error ? error.message : "unknown error",
    );
    return (
      <main className="page-shell vehicle-page-shell">
        <ApiUnavailable routePath={routePath} />
      </main>
    );
  }
}

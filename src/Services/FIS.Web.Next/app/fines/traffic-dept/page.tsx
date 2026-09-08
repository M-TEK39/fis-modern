import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import { deleteTrafficDeptAction } from "@/app/fines/actions";
import SessionRecovery from "@/app/home/session-recovery";
import { FineApiError, getTrafficDepts, type TrafficDeptRecord } from "@/lib/api-fines";
import { getSession } from "@/lib/session";

const REPORTS_ROLE = "Reports";

export type TrafficDeptPageProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
  routePath?: string;
};

function getQueryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

function hasReportsRole(roles: readonly string[]) {
  return roles.some(
    (role) => role.localeCompare(REPORTS_ROLE, undefined, { sensitivity: "accent" }) === 0,
  );
}

function valueOrDash(value: string | number | null | undefined) {
  return value === null || value === undefined || String(value).trim() === "" ? "-" : String(value);
}

function SearchForm({ searchQuery }: Readonly<{ searchQuery: string }>) {
  return (
    <form className="vehicle-status-maintenance-panel" method="get">
      <label className="form-label" htmlFor="traffic-dept-search">
        Traffic Dept Name
      </label>
      <div className="vehicle-search-row">
        <input
          className="vehicle-search"
          id="traffic-dept-search"
          maxLength={50}
          name="searchQuery"
          defaultValue={searchQuery}
        />
      </div>
      <div className="button-row">
        <button className="button button-primary" type="submit">
          Submit
        </button>
        <Link className="button button-secondary" href="/fines">
          Menu
        </Link>
      </div>
    </form>
  );
}

function DepartmentTable({ departments }: Readonly<{ departments: TrafficDeptRecord[] }>) {
  if (departments.length === 0) {
    return (
      <div className="vehicle-empty-state">
        <p className="eyebrow">No records found</p>
        <h2>No traffic departments matched.</h2>
        <p className="muted-copy">Add a new traffic department or search again.</p>
      </div>
    );
  }

  return (
    <div className="vehicle-table-wrapper" aria-live="polite">
      <table className="vehicle-table">
        <caption className="sr-only">Traffic departments</caption>
        <thead>
          <tr>
            <th scope="col">Traffic Dept Name</th>
            <th scope="col">Responsible Person</th>
            <th scope="col">Telephone</th>
            <th scope="col">Action</th>
          </tr>
        </thead>
        <tbody>
          {departments.map((dept) => (
            <tr key={dept.trafficDeptCode}>
              <td>{valueOrDash(dept.name)}</td>
              <td>{valueOrDash(dept.responsiblePerson)}</td>
              <td>{valueOrDash(dept.telephone)}</td>
              <td>
                <div className="button-row">
                  <Link
                    className="button button-secondary button-small"
                    href={`/fines/traffic-dept/detail?trafficDeptCode=${dept.trafficDeptCode}`}
                  >
                    Mod
                  </Link>
                  <form action={deleteTrafficDeptAction}>
                    <input name="trafficDeptCode" type="hidden" value={dept.trafficDeptCode} />
                    <button className="button button-danger button-small" type="submit">
                      Del
                    </button>
                  </form>
                </div>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

function ApiUnavailable() {
  return (
    <section className="vehicle-status-card" role="alert">
      <div className="status-icon status-icon-error" aria-hidden="true">
        !
      </div>
      <p className="eyebrow">API unavailable</p>
      <h2>Traffic department information could not be loaded.</h2>
      <p className="muted-copy">
        The application is still running. Retry when the FIS API is available.
      </p>
      <Link className="button button-primary" href="/fines/traffic-dept">
        Try again
      </Link>
    </section>
  );
}

export default async function TrafficDeptPage({
  searchParams,
  routePath = "/fines/traffic-dept",
}: TrafficDeptPageProps) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") {
    redirect("/login");
  }
  if (session.status === "expired") {
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath={routePath} />
      </main>
    );
  }
  if (session.status === "unavailable") {
    return (
      <main className="page-shell vehicle-page-shell">
        <ApiUnavailable />
      </main>
    );
  }
  if (!hasReportsRole(session.roles)) {
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <p className="eyebrow">Access restricted</p>
          <h2>You do not have permission to maintain Traffic Depts.</h2>
        </section>
      </main>
    );
  }

  const query = await searchParams;
  const searchQuery = (getQueryValue(query.searchQuery) ?? getQueryValue(query.xName) ?? "")
    .trim()
    .slice(0, 50);
  const error = getQueryValue(query.error);
  const notice =
    getQueryValue(query.saved) === "1"
      ? "Traffic department created successfully."
      : getQueryValue(query.updated) === "1"
        ? "Traffic department updated successfully."
        : getQueryValue(query.deleted) === "1"
          ? "Traffic department deleted successfully."
          : null;

  try {
    const allDepartments = await getTrafficDepts();
    const departments = searchQuery
      ? allDepartments.filter((dept) =>
          (dept.name ?? "").toLocaleLowerCase().includes(searchQuery.toLocaleLowerCase()),
        )
      : allDepartments;

    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-card" aria-labelledby="traffic-dept-title">
          <header className="vehicle-page-header">
            <div>
              <p className="eyebrow">Fines maintenance</p>
              <h1 id="traffic-dept-title">Traffic Dept Info Maintenance</h1>
              <p>Maintain the traffic-department fields used by fine records and reports.</p>
            </div>
            <Link className="button button-secondary" href="/fines">
              Fines Menu
            </Link>
          </header>
          {notice ? (
            <div className="notice notice-success" role="status">
              {notice}
            </div>
          ) : null}
          {error ? (
            <div className="notice notice-error" role="alert">
              {error}
            </div>
          ) : null}
          <SearchForm searchQuery={searchQuery} />
          <section
            className="vehicle-status-maintenance-panel"
            aria-labelledby="traffic-dept-results-title"
          >
            <div className="vehicle-form-section-header">
              <div>
                <p className="eyebrow">Traffic department</p>
                <h2 id="traffic-dept-results-title">Departments found</h2>
              </div>
              <Link
                className="button button-primary"
                href={`/fines/traffic-dept/detail${searchQuery ? `?name=${encodeURIComponent(searchQuery)}` : ""}`}
              >
                Add
              </Link>
            </div>
            <DepartmentTable departments={departments} />
          </section>
          <div className="vehicle-footer-actions">
            <Link className="button button-secondary" href="/home">
              Home
            </Link>
          </div>
        </section>
      </main>
    );
  } catch (errorValue) {
    if (errorValue instanceof FineApiError && errorValue.reason === "unauthorized") {
      return (
        <main className="page-shell vehicle-page-shell">
          <SessionRecovery returnPath={routePath} />
        </main>
      );
    }
    console.error(
      "FIS traffic department request failed",
      errorValue instanceof Error ? errorValue.message : "unknown error",
    );
    return (
      <main className="page-shell vehicle-page-shell">
        <ApiUnavailable />
      </main>
    );
  }
}

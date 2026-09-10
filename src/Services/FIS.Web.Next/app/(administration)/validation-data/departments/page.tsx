import Link from "next/link";
import { connection } from "next/server";
import { redirect } from "next/navigation";

import { logoutAction } from "@/app/(auth)/actions/auth";
import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import { hasVehicleManagementPermission } from "@/app/(administration)/drivers/access";
import {
  DepartmentApiError,
  getDepartments,
  type DepartmentRecord,
} from "@/lib/api/reference-data/api-departments";
import { getSession } from "@/lib/auth/session";

export type DepartmentListPageProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
  routePath?: string;
};

function getQueryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

function valueOrDash(value: string | number | null | undefined) {
  return value === null || value === undefined || String(value).trim() === "" ? "-" : String(value);
}

function editPath(departmentCode: number) {
  return `/Validation/MNT_Department_Edit.aspx?cmbdep=${encodeURIComponent(String(departmentCode))}`;
}

function deleteCheckPath(departmentCode: number) {
  return `/Validation/MNT_Department_Del_Check.aspx?code=${encodeURIComponent(String(departmentCode))}`;
}

function AccessRestricted() {
  return (
    <section className="vehicle-status-card" role="alert">
      <p className="eyebrow">Access restricted</p>
      <h2>You do not have permission to maintain departments.</h2>
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
      <h2>Department data could not be loaded.</h2>
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

function DepartmentTable({ departments }: Readonly<{ departments: DepartmentRecord[] }>) {
  if (departments.length === 0) {
    return (
      <div className="empty-state">
        <h2>No departments found</h2>
        <p>Add a department using the same legacy maintenance workflow.</p>
        <Link className="button button-primary" href="/Validation/MNT_Department_Add.aspx">
          Add Department
        </Link>
      </div>
    );
  }

  return (
    <div className="table-container">
      <div className="table-header">
        <span className="table-title">
          {departments.length} department{departments.length === 1 ? "" : "s"}
        </span>
      </div>
      <div className="table-wrapper">
        <table className="data-table">
          <caption className="sr-only">Legacy department records</caption>
          <thead>
            <tr>
              <th scope="col">Department number</th>
              <th scope="col">Description</th>
              <th scope="col">Responsible person</th>
              <th scope="col">Telephone</th>
              <th scope="col">Status</th>
              <th scope="col">Actions</th>
            </tr>
          </thead>
          <tbody>
            {departments.map((department) => (
              <tr key={department.departmentCode}>
                <td>{valueOrDash(department.departmentNumber)}</td>
                <td>{valueOrDash(department.description)}</td>
                <td>{valueOrDash(department.responsiblePerson)}</td>
                <td>{valueOrDash(department.telephone)}</td>
                <td>
                  <span
                    className={`badge ${department.deptActive ? "badge-success" : "badge-warning"}`}
                  >
                    {department.deptActive ? "Active" : "Inactive"}
                  </span>
                </td>
                <td className="actions-column">
                  <div className="table-actions">
                    <Link
                      className="button button-secondary button-small"
                      href={editPath(department.departmentCode)}
                    >
                      Edit
                    </Link>
                    <Link
                      className="button button-secondary button-small"
                      href={deleteCheckPath(department.departmentCode)}
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

export default async function DepartmentListPage({
  searchParams,
  routePath = "/validation-data/departments",
}: DepartmentListPageProps) {
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
  let departments: DepartmentRecord[] = [];

  try {
    departments = await getDepartments();
  } catch (caughtError) {
    if (caughtError instanceof DepartmentApiError && caughtError.reason === "unauthorized") {
      return (
        <main className="page-shell vehicle-page-shell">
          <SessionRecovery returnPath={routePath} />
        </main>
      );
    }
    console.error(
      "FIS department list request failed",
      caughtError instanceof Error ? caughtError.message : "unknown error",
    );
    return (
      <main className="page-shell vehicle-page-shell">
        <ApiUnavailable routePath={routePath} />
      </main>
    );
  }

  const notice =
    saved === "created"
      ? "Department added successfully."
      : saved === "updated"
        ? "Department updated successfully."
        : saved === "deleted"
          ? "Department deleted successfully."
          : error;

  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-labelledby="department-list-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Validation / Organisation</p>
            <h1 id="department-list-title">Department Maintenance</h1>
            <p>
              Maintain the complete legacy department record, including active and inactive
              departments.
            </p>
          </div>
          <div className="button-row">
            <Link className="button button-primary" href="/Validation/MNT_Department_Add.aspx">
              Add Department
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
        <DepartmentTable departments={departments} />
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
}

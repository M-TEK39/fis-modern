import Link from "next/link";
import { connection } from "next/server";
import { redirect } from "next/navigation";

import { logoutAction } from "@/app/actions/auth";
import { hasVehicleManagementPermission } from "@/app/drivers/access";
import SessionRecovery from "@/app/home/session-recovery";
import { ClassApiError, getClasses, type ClassRecord } from "@/lib/api-classes";
import { getSession } from "@/lib/session";

export type ClassListPageProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
  routePath?: string;
};

function getQueryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

function valueOrDash(value: string | number | null | undefined) {
  return value === null || value === undefined || String(value).trim() === "" ? "-" : String(value);
}

function editPath(classCode: number) {
  return `/Validation/MNT_Class_Edit.aspx?cmbClass=${encodeURIComponent(String(classCode))}`;
}

function deleteCheckPath(classCode: number) {
  return `/Validation/MNT_Class_Del_Check.aspx?code=${encodeURIComponent(String(classCode))}`;
}

function AccessRestricted() {
  return <section className="vehicle-status-card" role="alert"><p className="eyebrow">Access restricted</p><h2>You do not have permission to maintain vehicle classes.</h2><Link className="button button-secondary" href="/validation-data">Validation Data</Link></section>;
}

function ApiUnavailable({ routePath }: Readonly<{ routePath: string }>) {
  return <section className="vehicle-status-card" role="alert"><p className="eyebrow">API unavailable</p><h2>Vehicle classes could not be loaded.</h2><p className="muted-copy">The application is still running. Retry when the FIS API is available.</p><div className="button-row"><Link className="button button-primary" href={routePath}>Try again</Link><Link className="button button-secondary" href="/validation-data">Validation Data</Link></div></section>;
}

function ClassTable({ classes }: Readonly<{ classes: ClassRecord[] }>) {
  if (classes.length === 0) {
    return <div className="empty-state"><h2>No vehicle classes found</h2><p>Add a class using the same legacy vehicle-validation workflow.</p><Link className="button button-primary" href="/Validation/MNT_Class_Add.aspx">Add Class</Link></div>;
  }

  return (
    <div className="table-container">
      <div className="table-header"><span className="table-title">{classes.length} class{classes.length === 1 ? "" : "es"}</span></div>
      <div className="table-wrapper">
        <table className="data-table">
          <caption className="sr-only">Legacy vehicle classes</caption>
          <thead><tr><th scope="col">Class code</th><th scope="col">Description</th><th scope="col">Class number</th><th scope="col">Bank number</th><th scope="col">Months life</th><th scope="col">Actions</th></tr></thead>
          <tbody>
            {classes.map((classRecord) => (
              <tr key={classRecord.classCode}>
                <td>{classRecord.classCode}</td>
                <td>{valueOrDash(classRecord.description)}</td>
                <td>{valueOrDash(classRecord.classNumber)}</td>
                <td>{valueOrDash(classRecord.bankNumber)}</td>
                <td>{valueOrDash(classRecord.monthsLife)}</td>
                <td className="actions-column"><div className="table-actions"><Link className="button button-secondary button-small" href={editPath(classRecord.classCode)}>Edit</Link><Link className="button button-secondary button-small" href={deleteCheckPath(classRecord.classCode)}>Delete</Link></div></td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}

export default async function ClassListPage({ searchParams, routePath = "/validation-data/classes" }: ClassListPageProps) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired") return <main className="page-shell vehicle-page-shell"><SessionRecovery returnPath={routePath} /></main>;
  if (session.status === "unavailable") return <main className="page-shell vehicle-page-shell"><ApiUnavailable routePath={routePath} /></main>;
  if (!hasVehicleManagementPermission(session.accessLevel)) return <main className="page-shell vehicle-page-shell"><AccessRestricted /></main>;

  const query = await searchParams;
  const saved = getQueryValue(query.saved);
  const error = getQueryValue(query.error);
  try {
    const classes = await getClasses();
    const notice = saved === "created" ? "Class added successfully." : saved === "updated" ? "Class updated successfully." : saved === "deleted" ? "Class deleted successfully." : error;
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-card" aria-labelledby="class-list-title">
          <header className="vehicle-page-header"><div><p className="eyebrow">Validation / Vehicle</p><h1 id="class-list-title">Class Maintenance</h1><p>Maintain the complete legacy class record used by vehicle workflows.</p></div><div className="button-row"><Link className="button button-primary" href="/Validation/MNT_Class_Add.aspx">Add Class</Link><Link className="button button-secondary" href="/validation-data">Validation Data</Link></div></header>
          {notice ? <div className={`notice ${error ? "notice-error" : "notice-success"}`} role={error ? "alert" : "status"}>{notice}</div> : null}
          <ClassTable classes={classes} />
          <div className="vehicle-footer-actions"><Link className="button button-secondary" href="/home">Home</Link><form action={logoutAction}><button className="button button-secondary" type="submit">Sign out</button></form></div>
        </section>
      </main>
    );
  } catch (caughtError) {
    if (caughtError instanceof ClassApiError && caughtError.reason === "unauthorized") return <main className="page-shell vehicle-page-shell"><SessionRecovery returnPath={routePath} /></main>;
    console.error("FIS class list request failed", caughtError instanceof Error ? caughtError.message : "unknown error");
    return <main className="page-shell vehicle-page-shell"><ApiUnavailable routePath={routePath} /></main>;
  }
}

import Link from "next/link";
import { connection } from "next/server";
import { redirect } from "next/navigation";

import { hasVehicleManagementPermission } from "@/app/drivers/access";
import SessionRecovery from "@/app/home/session-recovery";
import { deleteDepartmentAction } from "@/app/validation-data/departments/actions";
import { DepartmentApiError, getDepartment, getDepartmentDeleteCheck } from "@/lib/api-departments";
import { getSession } from "@/lib/session";

type DepartmentDeleteCheckPageProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
};

function getQueryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

function parseCode(value: string | undefined) {
  const parsed = Number(value);
  return value && Number.isInteger(parsed) && parsed > 0 ? parsed : null;
}

function ErrorCard({ message }: Readonly<{ message: string }>) {
  return <section className="vehicle-status-card" role="alert"><p className="eyebrow">Department deletion</p><h2>{message}</h2><Link className="button button-secondary" href="/Validation/MNT_department.aspx">Department Maintenance</Link></section>;
}

export default async function DepartmentDeleteCheckPage({ searchParams }: DepartmentDeleteCheckPageProps) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired") return <main className="page-shell vehicle-page-shell"><SessionRecovery returnPath="/Validation/MNT_Department_Del_Check.aspx" /></main>;
  if (session.status === "unavailable") return <main className="page-shell vehicle-page-shell"><ErrorCard message="Department deletion is temporarily unavailable." /></main>;
  if (!hasVehicleManagementPermission(session.accessLevel)) return <main className="page-shell vehicle-page-shell"><ErrorCard message="You do not have permission to delete departments." /></main>;

  const query = await searchParams;
  const departmentCode = parseCode(getQueryValue(query.code) ?? getQueryValue(query.cmbdep) ?? getQueryValue(query.departmentCode));
  const queryError = getQueryValue(query.error);
  if (!departmentCode) return <main className="page-shell vehicle-page-shell"><ErrorCard message="Select a department before attempting deletion." /></main>;
  if (queryError) return <main className="page-shell vehicle-page-shell"><ErrorCard message={queryError} /></main>;

  try {
    const [department, dependencies] = await Promise.all([
      getDepartment(departmentCode),
      getDepartmentDeleteCheck(departmentCode),
    ]);
    if (!department) return <main className="page-shell vehicle-page-shell"><ErrorCard message={`Department ${departmentCode} was not found.`} /></main>;

    const blocked = dependencies.siteCount > 0 || dependencies.logsheetCount > 0;
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-card" aria-labelledby="department-delete-title">
          <header className="vehicle-page-header">
            <div><p className="eyebrow">Validation / Organisation</p><h1 id="department-delete-title">Delete Department</h1><p>Check the same site and logsheet dependencies used by the legacy delete flow.</p></div>
            <Link className="button button-secondary" href="/Validation/MNT_department.aspx">Department Maintenance</Link>
          </header>
          <section className="vehicle-status-card" role={blocked ? "alert" : "note"}>
            <p className="eyebrow">Department {department.departmentCode}</p>
            <h2>{department.description || `Department ${department.departmentCode}`}</h2>
            <dl className="status-maintenance-details">
              <div><dt>Sites assigned</dt><dd>{dependencies.siteCount}</dd></div>
              <div><dt>Logsheets using those sites</dt><dd>{dependencies.logsheetCount}</dd></div>
            </dl>
            {blocked ? <><p className="muted-copy">This department cannot be deleted until its sites are removed and its logsheets are rectified.</p><Link className="button button-secondary" href="/Validation/MNT_department.aspx">Return to Department Maintenance</Link></> : (
              <form action={deleteDepartmentAction} className="button-row">
                <input name="departmentCode" type="hidden" value={department.departmentCode} readOnly />
                <button className="button button-primary" type="submit">Confirm Delete</button>
                <Link className="button button-secondary" href="/Validation/MNT_department.aspx">Cancel</Link>
              </form>
            )}
          </section>
        </section>
      </main>
    );
  } catch (error) {
    if (error instanceof DepartmentApiError && error.reason === "unauthorized") return <main className="page-shell vehicle-page-shell"><SessionRecovery returnPath={`/Validation/MNT_Department_Del_Check.aspx?code=${departmentCode}`} /></main>;
    if (error instanceof DepartmentApiError && error.status === 404) return <main className="page-shell vehicle-page-shell"><ErrorCard message={`Department ${departmentCode} was not found.`} /></main>;
    console.error("FIS department delete check failed", error instanceof Error ? error.message : "unknown error");
    return <main className="page-shell vehicle-page-shell"><ErrorCard message="Department dependencies could not be checked." /></main>;
  }
}

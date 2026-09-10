import Link from "next/link";
import { connection } from "next/server";
import { redirect } from "next/navigation";

import { hasVehicleManagementPermission } from "@/app/(administration)/drivers/access";
import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import DepartmentForm from "@/app/(administration)/validation-data/departments/department-form";
import { createDepartmentAction } from "@/app/(administration)/validation-data/departments/actions";
import { emptyDepartment } from "@/app/(administration)/validation-data/departments/department-defaults";
import { getSession } from "@/lib/auth/session";

export default async function DepartmentAddPage() {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired")
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath="/Validation/MNT_Department_Add.aspx" />
      </main>
    );
  if (session.status === "unavailable")
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <p className="eyebrow">Service unavailable</p>
          <h2>Department maintenance could not be opened.</h2>
        </section>
      </main>
    );
  if (!hasVehicleManagementPermission(session.accessLevel))
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <p className="eyebrow">Access restricted</p>
          <h2>You do not have permission to add departments.</h2>
        </section>
      </main>
    );

  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-labelledby="department-add-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Validation / Organisation</p>
            <h1 id="department-add-title">Add Department</h1>
            <p>Capture the legacy department fields used by the original add screen.</p>
          </div>
          <Link className="button button-secondary" href="/Validation/MNT_department.aspx">
            Department Maintenance
          </Link>
        </header>
        <DepartmentForm
          action={createDepartmentAction}
          department={emptyDepartment}
          mode="create"
          returnPath="/Validation/MNT_department.aspx"
        />
      </section>
    </main>
  );
}

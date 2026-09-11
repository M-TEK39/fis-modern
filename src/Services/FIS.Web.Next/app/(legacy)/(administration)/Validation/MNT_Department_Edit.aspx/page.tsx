import Link from "next/link";
import { connection } from "next/server";
import { Suspense } from "react";
import RouteLoading from "@/components/app-shell/route-loading";
import { redirect } from "next/navigation";
import type { ReactNode } from "react";

import { hasVehicleManagementPermission } from "@/app/(administration)/drivers/access";
import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import DepartmentForm from "@/app/(administration)/validation-data/departments/department-form";
import { updateDepartmentAction } from "@/app/(administration)/validation-data/departments/actions";
import { DepartmentApiError, getDepartment } from "@/lib/api/reference-data/api-departments";
import { getSession } from "@/lib/auth/session";

type DepartmentEditPageProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
};

function getQueryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

function parseCode(value: string | undefined) {
  const parsed = Number(value);
  return value && Number.isInteger(parsed) && parsed > 0 ? parsed : null;
}

function ErrorCard({ children }: Readonly<{ children: ReactNode }>) {
  return (
    <section className="vehicle-status-card" role="alert">
      <p className="eyebrow">Department maintenance</p>
      <h2>{children}</h2>
      <Link className="button button-secondary" href="/Validation/MNT_department.aspx">
        Department Maintenance
      </Link>
    </section>
  );
}

async function DepartmentEditPageContent({ searchParams }: DepartmentEditPageProps) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired")
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath="/Validation/MNT_Department_Edit.aspx" />
      </main>
    );
  if (session.status === "unavailable")
    return (
      <main className="page-shell vehicle-page-shell">
        <ErrorCard>Department maintenance is temporarily unavailable.</ErrorCard>
      </main>
    );
  if (!hasVehicleManagementPermission(session.accessLevel))
    return (
      <main className="page-shell vehicle-page-shell">
        <ErrorCard>You do not have permission to edit departments.</ErrorCard>
      </main>
    );

  const query = await searchParams;
  const departmentCode = parseCode(
    getQueryValue(query.cmbdep) ?? getQueryValue(query.code) ?? getQueryValue(query.departmentCode),
  );
  if (!departmentCode)
    return (
      <main className="page-shell vehicle-page-shell">
        <ErrorCard>Select a department before opening edit.</ErrorCard>
      </main>
    );

  try {
    const department = await getDepartment(departmentCode);
    if (!department)
      return (
        <main className="page-shell vehicle-page-shell">
          <ErrorCard>Department {departmentCode} was not found.</ErrorCard>
        </main>
      );

    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-card" aria-labelledby="department-edit-title">
          <header className="vehicle-page-header">
            <div>
              <p className="eyebrow">Validation / Organisation</p>
              <h1 id="department-edit-title">Edit Department</h1>
              <p>
                Update department {departmentCode} without dropping legacy or expanded-schema
                values.
              </p>
            </div>
            <Link className="button button-secondary" href="/Validation/MNT_department.aspx">
              Department Maintenance
            </Link>
          </header>
          <DepartmentForm
            action={updateDepartmentAction}
            department={department}
            mode="update"
            returnPath="/Validation/MNT_department.aspx"
          />
        </section>
      </main>
    );
  } catch (error) {
    if (error instanceof DepartmentApiError && error.reason === "unauthorized")
      return (
        <main className="page-shell vehicle-page-shell">
          <SessionRecovery
            returnPath={`/Validation/MNT_Department_Edit.aspx?cmbdep=${departmentCode}`}
          />
        </main>
      );
    if (error instanceof DepartmentApiError && error.status === 404)
      return (
        <main className="page-shell vehicle-page-shell">
          <ErrorCard>Department {departmentCode} was not found.</ErrorCard>
        </main>
      );
    console.error(
      "FIS department edit request failed",
      error instanceof Error ? error.message : "unknown error",
    );
    return (
      <main className="page-shell vehicle-page-shell">
        <ErrorCard>Department details could not be loaded.</ErrorCard>
      </main>
    );
  }
}

export default function DepartmentEditPage(
  props: NonNullable<Parameters<typeof DepartmentEditPageContent>[0]>,
) {
  return (
    <Suspense fallback={<RouteLoading />}>
      <DepartmentEditPageContent {...props} />
    </Suspense>
  );
}

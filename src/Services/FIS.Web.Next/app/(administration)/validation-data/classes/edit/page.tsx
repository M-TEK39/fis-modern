import Link from "next/link";
import { connection } from "next/server";
import { redirect } from "next/navigation";
import { Suspense } from "react";

import { hasVehicleManagementPermission } from "@/app/(administration)/drivers/access";
import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import { updateClassAction } from "@/app/(administration)/validation-data/classes/actions";
import ClassForm from "@/app/(administration)/validation-data/classes/class-form";
import { ClassApiError, getClass } from "@/lib/api/reference-data/api-classes";
import { getSession } from "@/lib/auth/session";
import RouteLoading from "@/components/app-shell/route-loading";

type ClassEditPageProps = { searchParams: Promise<Record<string, string | string[] | undefined>> };

function getQueryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

function parseCode(value: string | undefined) {
  const parsed = Number(value);
  return value && Number.isInteger(parsed) && parsed > 0 && parsed <= 32767 ? parsed : null;
}

function ErrorCard({ message }: Readonly<{ message: string }>) {
  return (
    <section className="vehicle-status-card" role="alert">
      <p className="eyebrow">Class maintenance</p>
      <h2>{message}</h2>
      <Link className="button button-secondary" href="/Validation/MNT_Class.aspx">
        Class Maintenance
      </Link>
    </section>
  );
}

async function ClassEditPageContent({ searchParams }: ClassEditPageProps) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired")
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath="/Validation/MNT_Class_Edit.aspx" />
      </main>
    );
  if (session.status === "unavailable")
    return (
      <main className="page-shell vehicle-page-shell">
        <ErrorCard message="Class maintenance is temporarily unavailable." />
      </main>
    );
  if (!hasVehicleManagementPermission(session.accessLevel))
    return (
      <main className="page-shell vehicle-page-shell">
        <ErrorCard message="You do not have permission to edit vehicle classes." />
      </main>
    );

  const query = await searchParams;
  const classCode = parseCode(
    getQueryValue(query.cmbClass) ??
      getQueryValue(query.cmbclass) ??
      getQueryValue(query.classCode) ??
      getQueryValue(query.code),
  );
  if (!classCode)
    return (
      <main className="page-shell vehicle-page-shell">
        <ErrorCard message="Select a class before opening edit." />
      </main>
    );

  try {
    const classRecord = await getClass(classCode);
    if (!classRecord)
      return (
        <main className="page-shell vehicle-page-shell">
          <ErrorCard message={`Class ${classCode} was not found.`} />
        </main>
      );
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-card" aria-labelledby="class-edit-title">
          <header className="vehicle-page-header">
            <div>
              <p className="eyebrow">Validation / Vehicle</p>
              <h1 id="class-edit-title">Edit Class</h1>
              <p>Update class {classCode} without dropping any legacy class fields.</p>
            </div>
            <Link className="button button-secondary" href="/Validation/MNT_Class.aspx">
              Class Maintenance
            </Link>
          </header>
          <ClassForm action={updateClassAction} classRecord={classRecord} mode="update" />
        </section>
      </main>
    );
  } catch (error) {
    if (error instanceof ClassApiError && error.reason === "unauthorized")
      return (
        <main className="page-shell vehicle-page-shell">
          <SessionRecovery returnPath={`/Validation/MNT_Class_Edit.aspx?cmbClass=${classCode}`} />
        </main>
      );
    if (error instanceof ClassApiError && error.status === 404)
      return (
        <main className="page-shell vehicle-page-shell">
          <ErrorCard message={`Class ${classCode} was not found.`} />
        </main>
      );
    console.error(
      "FIS class edit request failed",
      error instanceof Error ? error.message : "unknown error",
    );
    return (
      <main className="page-shell vehicle-page-shell">
        <ErrorCard message="Class details could not be loaded." />
      </main>
    );
  }
}

export default function ClassEditPage(
  props: NonNullable<Parameters<typeof ClassEditPageContent>[0]>,
) {
  return (
    <Suspense fallback={<RouteLoading />}>
      <ClassEditPageContent {...props} />
    </Suspense>
  );
}

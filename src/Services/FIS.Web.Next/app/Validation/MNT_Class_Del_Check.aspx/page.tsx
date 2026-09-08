import Link from "next/link";
import { connection } from "next/server";
import { redirect } from "next/navigation";

import { hasVehicleManagementPermission } from "@/app/drivers/access";
import SessionRecovery from "@/app/home/session-recovery";
import { deleteClassAction } from "@/app/validation-data/classes/actions";
import { ClassApiError, getClass, getClassDeleteCheck } from "@/lib/api-classes";
import { getSession } from "@/lib/session";

type ClassDeleteCheckPageProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
};

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
      <p className="eyebrow">Class deletion</p>
      <h2>{message}</h2>
      <Link className="button button-secondary" href="/Validation/MNT_Class.aspx">
        Class Maintenance
      </Link>
    </section>
  );
}

export default async function ClassDeleteCheckPage({ searchParams }: ClassDeleteCheckPageProps) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired")
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath="/Validation/MNT_Class_Del_Check.aspx" />
      </main>
    );
  if (session.status === "unavailable")
    return (
      <main className="page-shell vehicle-page-shell">
        <ErrorCard message="Class deletion is temporarily unavailable." />
      </main>
    );
  if (!hasVehicleManagementPermission(session.accessLevel))
    return (
      <main className="page-shell vehicle-page-shell">
        <ErrorCard message="You do not have permission to delete vehicle classes." />
      </main>
    );

  const query = await searchParams;
  const classCode = parseCode(getQueryValue(query.code) ?? getQueryValue(query.classCode));
  const error = getQueryValue(query.error);
  if (!classCode)
    return (
      <main className="page-shell vehicle-page-shell">
        <ErrorCard message="Select a class before attempting deletion." />
      </main>
    );
  if (error)
    return (
      <main className="page-shell vehicle-page-shell">
        <ErrorCard message={error} />
      </main>
    );

  try {
    const [classRecord, dependencies] = await Promise.all([
      getClass(classCode),
      getClassDeleteCheck(classCode),
    ]);
    if (!classRecord)
      return (
        <main className="page-shell vehicle-page-shell">
          <ErrorCard message={`Class ${classCode} was not found.`} />
        </main>
      );
    const blocked =
      !dependencies.canDelete || dependencies.modelCount > 0 || dependencies.vehicleCount > 0;
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-card" aria-labelledby="class-delete-title">
          <header className="vehicle-page-header">
            <div>
              <p className="eyebrow">Validation / Vehicle</p>
              <h1 id="class-delete-title">Delete Class</h1>
              <p>Check linked models and vehicles before deleting this class.</p>
            </div>
            <Link className="button button-secondary" href="/Validation/MNT_Class.aspx">
              Class Maintenance
            </Link>
          </header>
          <section className="vehicle-status-card" role={blocked ? "alert" : "note"}>
            <p className="eyebrow">Class {classRecord.classCode}</p>
            <h2>{classRecord.description || `Class ${classRecord.classCode}`}</h2>
            <dl className="status-maintenance-details">
              <div>
                <dt>Linked models</dt>
                <dd>{dependencies.modelCount}</dd>
              </div>
              <div>
                <dt>Linked vehicles</dt>
                <dd>{dependencies.vehicleCount}</dd>
              </div>
            </dl>
            {blocked ? (
              <>
                <p className="muted-copy">
                  Models and vehicles assigned to this class must be changed before deleting it.
                </p>
                <Link className="button button-secondary" href="/Validation/MNT_Class.aspx">
                  Return to Class Maintenance
                </Link>
              </>
            ) : (
              <form action={deleteClassAction} className="button-row">
                <input name="classCode" type="hidden" value={classRecord.classCode} readOnly />
                <button className="button button-primary" type="submit">
                  Confirm Delete
                </button>
                <Link className="button button-secondary" href="/Validation/MNT_Class.aspx">
                  Cancel
                </Link>
              </form>
            )}
          </section>
        </section>
      </main>
    );
  } catch (caughtError) {
    if (caughtError instanceof ClassApiError && caughtError.reason === "unauthorized")
      return (
        <main className="page-shell vehicle-page-shell">
          <SessionRecovery returnPath={`/Validation/MNT_Class_Del_Check.aspx?code=${classCode}`} />
        </main>
      );
    if (caughtError instanceof ClassApiError && caughtError.status === 404)
      return (
        <main className="page-shell vehicle-page-shell">
          <ErrorCard message={`Class ${classCode} was not found.`} />
        </main>
      );
    console.error(
      "FIS class delete check failed",
      caughtError instanceof Error ? caughtError.message : "unknown error",
    );
    return (
      <main className="page-shell vehicle-page-shell">
        <ErrorCard message="Class dependencies could not be checked." />
      </main>
    );
  }
}

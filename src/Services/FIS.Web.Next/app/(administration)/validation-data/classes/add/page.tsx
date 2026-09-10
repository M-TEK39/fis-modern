import Link from "next/link";
import { connection } from "next/server";
import { redirect } from "next/navigation";

import { hasVehicleManagementPermission } from "@/app/(administration)/drivers/access";
import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import { createClassAction } from "@/app/(administration)/validation-data/classes/actions";
import ClassForm from "@/app/(administration)/validation-data/classes/class-form";
import { ClassApiError, type ClassRecord } from "@/lib/api/reference-data/api-classes";
import { getSession } from "@/lib/auth/session";

const emptyClass: ClassRecord = {
  classCode: 0,
  description: "",
  classNumber: "",
  bankNumber: "",
  monthsLife: 0,
  depreciationPercent: 0,
  odometerLife: 0,
  appreciatePercent: 0,
  replacementCost: 0,
  dateCreated: null,
  dateUpdated: null,
  createdByUserCode: null,
  modifiedByUserCode: null,
  isDeleted: false,
};

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

export default async function ClassAddPage() {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired")
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath="/Validation/MNT_Class_Add.aspx" />
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
        <ErrorCard message="You do not have permission to add vehicle classes." />
      </main>
    );

  try {
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-card" aria-labelledby="class-add-title">
          <header className="vehicle-page-header">
            <div>
              <p className="eyebrow">Validation / Vehicle</p>
              <h1 id="class-add-title">Add New Class</h1>
              <p>Add a complete vehicle class record to the legacy class table.</p>
            </div>
            <Link className="button button-secondary" href="/Validation/MNT_Class.aspx">
              Class Maintenance
            </Link>
          </header>
          <ClassForm action={createClassAction} classRecord={emptyClass} mode="create" />
        </section>
      </main>
    );
  } catch (error) {
    if (error instanceof ClassApiError && error.reason === "unauthorized")
      return (
        <main className="page-shell vehicle-page-shell">
          <SessionRecovery returnPath="/Validation/MNT_Class_Add.aspx" />
        </main>
      );
    console.error(
      "FIS class add request failed",
      error instanceof Error ? error.message : "unknown error",
    );
    return (
      <main className="page-shell vehicle-page-shell">
        <ErrorCard message="Class maintenance could not be loaded." />
      </main>
    );
  }
}

import Link from "next/link";
import { connection } from "next/server";
import { redirect } from "next/navigation";

import { hasVehicleManagementPermission } from "@/app/(administration)/drivers/access";
import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import { createLossTypeAction } from "@/app/(administration)/validation-data/loss-types/actions";
import LossTypeForm from "@/app/(administration)/validation-data/loss-types/loss-type-form";
import { LossTypeApiError, type LossTypeRecord } from "@/lib/api/fleet-operations/api-loss-types";
import { getSession } from "@/lib/auth/session";

const emptyLossType: LossTypeRecord = {
  lossTypeCode: 0,
  description: "",
  dateCreated: null,
  dateUpdated: null,
  createdByUserCode: null,
  modifiedByUserCode: null,
  isDeleted: false,
};

function ErrorCard({ message }: Readonly<{ message: string }>) {
  return (
    <section className="vehicle-status-card" role="alert">
      <p className="eyebrow">Loss description maintenance</p>
      <h2>{message}</h2>
      <Link className="button button-secondary" href="/Validation/MNT_Loss_Type.aspx">
        Loss Description Maintenance
      </Link>
    </section>
  );
}

export default async function LossTypeAddPage() {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired")
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath="/Validation/MNT_Loss_Type_Add.aspx" />
      </main>
    );
  if (session.status === "unavailable")
    return (
      <main className="page-shell vehicle-page-shell">
        <ErrorCard message="Loss description maintenance is temporarily unavailable." />
      </main>
    );
  if (!hasVehicleManagementPermission(session.accessLevel))
    return (
      <main className="page-shell vehicle-page-shell">
        <ErrorCard message="You do not have permission to add loss descriptions." />
      </main>
    );

  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-labelledby="loss-type-add-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Validation / Operational</p>
            <h1 id="loss-type-add-title">Add New Loss Description</h1>
            <p>Add a loss description to the legacy validation table.</p>
          </div>
          <Link className="button button-secondary" href="/Validation/MNT_Loss_Type.aspx">
            Loss Description Maintenance
          </Link>
        </header>
        <LossTypeForm action={createLossTypeAction} lossType={emptyLossType} mode="create" />
      </section>
    </main>
  );
}

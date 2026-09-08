import Link from "next/link";
import { connection } from "next/server";
import { redirect } from "next/navigation";

import { hasVehicleManagementPermission } from "@/app/drivers/access";
import SessionRecovery from "@/app/home/session-recovery";
import { createLicenseFeeAction } from "@/app/validation-data/license-fees/actions";
import LicenseFeeForm from "@/app/validation-data/license-fees/license-fee-form";
import { LicenseFeeApiError, type LicenseFeeRecord } from "@/lib/api-license-fees";
import { getSession } from "@/lib/session";

const emptyFee: LicenseFeeRecord = {
  licenceFeeCode: 0,
  description: "",
  fee: null,
  dateCreated: null,
  dateUpdated: null,
  createdByUserCode: null,
  modifiedByUserCode: null,
  isDeleted: false,
};

function ErrorCard({ message }: Readonly<{ message: string }>) {
  return (
    <section className="vehicle-status-card" role="alert">
      <p className="eyebrow">Licence fee maintenance</p>
      <h2>{message}</h2>
      <Link className="button button-secondary" href="/Validation/MNT_Licence_Fees.aspx">
        Licence Fees Maintenance
      </Link>
    </section>
  );
}

export default async function LicenseFeeAddPage() {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired")
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath="/Validation/MNT_Licence_Fee_Add.aspx" />
      </main>
    );
  if (session.status === "unavailable")
    return (
      <main className="page-shell vehicle-page-shell">
        <ErrorCard message="Licence fee maintenance is temporarily unavailable." />
      </main>
    );
  if (!hasVehicleManagementPermission(session.accessLevel))
    return (
      <main className="page-shell vehicle-page-shell">
        <ErrorCard message="You do not have permission to add licence fees." />
      </main>
    );
  try {
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-card" aria-labelledby="license-fee-add-title">
          <header className="vehicle-page-header">
            <div>
              <p className="eyebrow">Validation / Licence</p>
              <h1 id="license-fee-add-title">Add New Licence Fee</h1>
              <p>Add a licence fee to the legacy validation table.</p>
            </div>
            <Link className="button button-secondary" href="/Validation/MNT_Licence_Fees.aspx">
              Licence Fees Maintenance
            </Link>
          </header>
          <LicenseFeeForm action={createLicenseFeeAction} fee={emptyFee} mode="create" />
        </section>
      </main>
    );
  } catch (error) {
    if (error instanceof LicenseFeeApiError && error.reason === "unauthorized")
      return (
        <main className="page-shell vehicle-page-shell">
          <SessionRecovery returnPath="/Validation/MNT_Licence_Fee_Add.aspx" />
        </main>
      );
    return (
      <main className="page-shell vehicle-page-shell">
        <ErrorCard message="Licence fee maintenance could not be loaded." />
      </main>
    );
  }
}

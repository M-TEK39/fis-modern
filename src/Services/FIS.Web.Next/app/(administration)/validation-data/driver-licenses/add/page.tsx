import Link from "next/link";
import { connection } from "next/server";
import { redirect } from "next/navigation";

import { hasVehicleManagementPermission } from "@/app/(administration)/drivers/access";
import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import { createDriverLicenceAction } from "@/app/(administration)/validation-data/driver-licenses/actions";
import DriverLicenceForm from "@/app/(administration)/validation-data/driver-licenses/driver-licence-form";
import {
  DriverLicenceApiError,
  type DriverLicenceRecord,
} from "@/lib/api/reference-data/api-driver-licences";
import { getSession } from "@/lib/auth/session";

const emptyLicence: DriverLicenceRecord = {
  licenceCode: 0,
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
      <p className="eyebrow">Driver licence maintenance</p>
      <h2>{message}</h2>
      <Link className="button button-secondary" href="/Validation/MNT_DriversLicence.aspx">
        Driver Licence Maintenance
      </Link>
    </section>
  );
}

export default async function DriverLicenceAddPage() {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired")
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath="/Validation/MNT_Driverslicence_Add.aspx" />
      </main>
    );
  if (session.status === "unavailable")
    return (
      <main className="page-shell vehicle-page-shell">
        <ErrorCard message="Driver licence maintenance is temporarily unavailable." />
      </main>
    );
  if (!hasVehicleManagementPermission(session.accessLevel))
    return (
      <main className="page-shell vehicle-page-shell">
        <ErrorCard message="You do not have permission to add driver licences." />
      </main>
    );

  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-labelledby="driver-licence-add-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Validation / Licence</p>
            <h1 id="driver-licence-add-title">Add New Driver Licence</h1>
            <p>Add a driver licence description to the legacy validation table.</p>
          </div>
          <Link className="button button-secondary" href="/Validation/MNT_DriversLicence.aspx">
            Driver Licence Maintenance
          </Link>
        </header>
        <DriverLicenceForm
          action={createDriverLicenceAction}
          licence={emptyLicence}
          mode="create"
        />
      </section>
    </main>
  );
}

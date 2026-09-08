import Link from "next/link";
import { connection } from "next/server";
import { redirect } from "next/navigation";

import { hasVehicleManagementPermission } from "@/app/drivers/access";
import SessionRecovery from "@/app/home/session-recovery";
import { updateDriverLicenceAction } from "@/app/validation-data/driver-licenses/actions";
import DriverLicenceForm from "@/app/validation-data/driver-licenses/driver-licence-form";
import { DriverLicenceApiError, getDriverLicence } from "@/lib/api-driver-licences";
import { getSession } from "@/lib/session";

type DriverLicenceEditPageProps = {
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
      <p className="eyebrow">Driver licence maintenance</p>
      <h2>{message}</h2>
      <Link className="button button-secondary" href="/Validation/MNT_DriversLicence.aspx">
        Driver Licence Maintenance
      </Link>
    </section>
  );
}

export default async function DriverLicenceEditPage({ searchParams }: DriverLicenceEditPageProps) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired")
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath="/Validation/MNT_DriversLicence_Edit.aspx" />
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
        <ErrorCard message="You do not have permission to edit driver licences." />
      </main>
    );

  const query = await searchParams;
  const licenceCode = parseCode(
    getQueryValue(query.cmbDriverL) ??
      getQueryValue(query.cmbdriverl) ??
      getQueryValue(query.licenceCode) ??
      getQueryValue(query.code),
  );
  if (!licenceCode)
    return (
      <main className="page-shell vehicle-page-shell">
        <ErrorCard message="Select a driver licence before opening edit." />
      </main>
    );

  try {
    const licence = await getDriverLicence(licenceCode);
    if (!licence)
      return (
        <main className="page-shell vehicle-page-shell">
          <ErrorCard message={`Driver licence ${licenceCode} was not found.`} />
        </main>
      );
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-card" aria-labelledby="driver-licence-edit-title">
          <header className="vehicle-page-header">
            <div>
              <p className="eyebrow">Validation / Licence</p>
              <h1 id="driver-licence-edit-title">Edit Driver Licence</h1>
              <p>Update driver licence {licenceCode} without changing its legacy code.</p>
            </div>
            <Link className="button button-secondary" href="/Validation/MNT_DriversLicence.aspx">
              Driver Licence Maintenance
            </Link>
          </header>
          <DriverLicenceForm action={updateDriverLicenceAction} licence={licence} mode="update" />
        </section>
      </main>
    );
  } catch (error) {
    if (error instanceof DriverLicenceApiError && error.reason === "unauthorized")
      return (
        <main className="page-shell vehicle-page-shell">
          <SessionRecovery
            returnPath={`/Validation/MNT_DriversLicence_Edit.aspx?cmbDriverL=${licenceCode}`}
          />
        </main>
      );
    if (error instanceof DriverLicenceApiError && error.status === 404)
      return (
        <main className="page-shell vehicle-page-shell">
          <ErrorCard message={`Driver licence ${licenceCode} was not found.`} />
        </main>
      );
    console.error(
      "FIS driver licence edit request failed",
      error instanceof Error ? error.message : "unknown error",
    );
    return (
      <main className="page-shell vehicle-page-shell">
        <ErrorCard message="Driver licence details could not be loaded." />
      </main>
    );
  }
}

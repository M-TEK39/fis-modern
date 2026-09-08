import Link from "next/link";
import { connection } from "next/server";
import { redirect } from "next/navigation";

import { hasVehicleManagementPermission } from "@/app/drivers/access";
import SessionRecovery from "@/app/home/session-recovery";
import { deleteDriverLicenceAction } from "@/app/validation-data/driver-licenses/actions";
import {
  DriverLicenceApiError,
  getDriverLicence,
  getDriverLicenceDeleteCheck,
} from "@/lib/api-driver-licences";
import { getSession } from "@/lib/session";

type DriverLicenceDeleteCheckPageProps = {
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

export default async function DriverLicenceDeleteCheckPage({
  searchParams,
}: DriverLicenceDeleteCheckPageProps) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired")
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath="/Validation/MNT_DriversLicence_Del_Check.aspx" />
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
        <ErrorCard message="You do not have permission to delete driver licences." />
      </main>
    );

  const query = await searchParams;
  const code = parseCode(getQueryValue(query.code) ?? getQueryValue(query.licenceCode));
  const error = getQueryValue(query.error);
  if (!code)
    return (
      <main className="page-shell vehicle-page-shell">
        <ErrorCard message="Select a driver licence before deleting it." />
      </main>
    );

  try {
    const [licence, dependencies] = await Promise.all([
      getDriverLicence(code),
      getDriverLicenceDeleteCheck(code),
    ]);
    if (!licence)
      return (
        <main className="page-shell vehicle-page-shell">
          <ErrorCard message={`Driver licence ${code} was not found.`} />
        </main>
      );
    const blocked = !dependencies.canDelete || dependencies.modelCount > 0;
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-card" aria-labelledby="driver-licence-delete-title">
          <header className="vehicle-page-header">
            <div>
              <p className="eyebrow">Validation / Licence</p>
              <h1 id="driver-licence-delete-title">Delete Driver Licence</h1>
              <p>Check linked vehicle models before deleting this driver licence.</p>
            </div>
            <Link className="button button-secondary" href="/Validation/MNT_DriversLicence.aspx">
              Driver Licence Maintenance
            </Link>
          </header>
          {error ? (
            <div className="notice notice-error" role="alert">
              {error}
            </div>
          ) : null}
          <section className="vehicle-status-card" role={blocked ? "alert" : "note"}>
            <p className="eyebrow">Licence {licence.licenceCode}</p>
            <h2>{licence.description || `Driver licence ${licence.licenceCode}`}</h2>
            <dl className="status-maintenance-details">
              <div>
                <dt>Linked vehicle models</dt>
                <dd>{dependencies.modelCount}</dd>
              </div>
            </dl>
            {blocked ? (
              <>
                <p className="muted-copy">
                  Models using this driver licence must be changed before deleting it.
                </p>
                <Link
                  className="button button-secondary"
                  href="/Validation/MNT_DriversLicence.aspx"
                >
                  Return to Driver Licence Maintenance
                </Link>
              </>
            ) : (
              <form action={deleteDriverLicenceAction} className="button-row">
                <input name="licenceCode" type="hidden" value={licence.licenceCode} readOnly />
                <button className="button button-primary" type="submit">
                  Confirm Delete
                </button>
                <Link
                  className="button button-secondary"
                  href="/Validation/MNT_DriversLicence.aspx"
                >
                  Cancel
                </Link>
              </form>
            )}
          </section>
        </section>
      </main>
    );
  } catch (caughtError) {
    if (caughtError instanceof DriverLicenceApiError && caughtError.reason === "unauthorized")
      return (
        <main className="page-shell vehicle-page-shell">
          <SessionRecovery
            returnPath={`/Validation/MNT_DriversLicence_Del_Check.aspx?code=${code}`}
          />
        </main>
      );
    if (caughtError instanceof DriverLicenceApiError && caughtError.status === 404)
      return (
        <main className="page-shell vehicle-page-shell">
          <ErrorCard message={`Driver licence ${code} was not found.`} />
        </main>
      );
    console.error(
      "FIS driver licence delete-check request failed",
      caughtError instanceof Error ? caughtError.message : "unknown error",
    );
    return (
      <main className="page-shell vehicle-page-shell">
        <ErrorCard message="Driver licence dependencies could not be checked." />
      </main>
    );
  }
}

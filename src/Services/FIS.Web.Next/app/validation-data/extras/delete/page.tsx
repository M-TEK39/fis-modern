import Link from "next/link";
import { connection } from "next/server";
import { redirect } from "next/navigation";

import { hasVehicleManagementPermission } from "@/app/drivers/access";
import SessionRecovery from "@/app/home/session-recovery";
import { deleteExtraCodeAction } from "@/app/validation-data/extras/actions";
import { ExtraCodeApiError, getExtraCode, getExtraCodeDeleteCheck } from "@/lib/api-extra-codes";
import { getSession } from "@/lib/session";

type ExtraCodeDeletePageProps = {
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
      <p className="eyebrow">Extra deletion</p>
      <h2>{message}</h2>
      <Link className="button button-secondary" href="/Validation/MNT_Extras.aspx">
        Optional Extras Maintenance
      </Link>
    </section>
  );
}

export default async function ExtraCodeDeletePage({ searchParams }: ExtraCodeDeletePageProps) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired")
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath="/validation-data/extras/delete" />
      </main>
    );
  if (session.status === "unavailable")
    return (
      <main className="page-shell vehicle-page-shell">
        <ErrorCard message="Extra deletion is temporarily unavailable." />
      </main>
    );
  if (!hasVehicleManagementPermission(session.accessLevel))
    return (
      <main className="page-shell vehicle-page-shell">
        <ErrorCard message="You do not have permission to delete extras." />
      </main>
    );

  const query = await searchParams;
  const code = parseCode(getQueryValue(query.code));
  const error = getQueryValue(query.error);
  if (!code)
    return (
      <main className="page-shell vehicle-page-shell">
        <ErrorCard message="Select an extra before attempting deletion." />
      </main>
    );
  if (error)
    return (
      <main className="page-shell vehicle-page-shell">
        <ErrorCard message={error} />
      </main>
    );

  try {
    const extraPromise = getExtraCode(code);
    const dependencyPromise = getExtraCodeDeleteCheck(code);
    const [extra, dependencies] = await Promise.all([extraPromise, dependencyPromise]);
    if (!extra)
      return (
        <main className="page-shell vehicle-page-shell">
          <ErrorCard message={`Extra code ${code} was not found.`} />
        </main>
      );

    const blocked =
      !dependencies.checkAvailable || !dependencies.canDelete || dependencies.vehicleCount > 0;
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-card" aria-labelledby="extra-code-delete-title">
          <header className="vehicle-page-header">
            <div>
              <p className="eyebrow">Validation / Vehicle</p>
              <h1 id="extra-code-delete-title">Delete Extra</h1>
              <p>Check linked vehicle data before deleting this optional extra.</p>
            </div>
            <Link className="button button-secondary" href="/Validation/MNT_Extras.aspx">
              Optional Extras Maintenance
            </Link>
          </header>
          <section className="vehicle-status-card" role={blocked ? "alert" : "note"}>
            <p className="eyebrow">Extra code {extra.extraCode}</p>
            <h2>{extra.description || `Extra code ${extra.extraCode}`}</h2>
            {!dependencies.checkAvailable ? (
              <p className="muted-copy">
                Linked vehicle data could not be verified, so this extra cannot be deleted yet.
              </p>
            ) : dependencies.vehicleCount > 0 ? (
              <>
                <p className="muted-copy">
                  Remove this extra from the following vehicles before deleting it:
                </p>
                <ul>
                  {dependencies.fleetNumbers.map((fleetNumber) => (
                    <li key={fleetNumber}>{fleetNumber}</li>
                  ))}
                </ul>
              </>
            ) : (
              <p className="muted-copy">
                This extra is not linked to any vehicle data. Deleting it cannot be undone.
              </p>
            )}
            {blocked ? (
              <Link className="button button-secondary" href="/Validation/MNT_Extras.aspx">
                Return to Optional Extras Maintenance
              </Link>
            ) : (
              <form action={deleteExtraCodeAction} className="button-row">
                <input name="extraCode" type="hidden" value={extra.extraCode} readOnly />
                <button className="button button-primary" type="submit">
                  Confirm Delete
                </button>
                <Link className="button button-secondary" href="/Validation/MNT_Extras.aspx">
                  Cancel
                </Link>
              </form>
            )}
          </section>
        </section>
      </main>
    );
  } catch (caughtError) {
    if (caughtError instanceof ExtraCodeApiError && caughtError.reason === "unauthorized")
      return (
        <main className="page-shell vehicle-page-shell">
          <SessionRecovery returnPath={`/validation-data/extras/delete?code=${code}`} />
        </main>
      );
    if (caughtError instanceof ExtraCodeApiError && caughtError.status === 404)
      return (
        <main className="page-shell vehicle-page-shell">
          <ErrorCard message={`Extra code ${code} was not found.`} />
        </main>
      );
    console.error(
      "FIS extra code delete check failed",
      caughtError instanceof Error ? caughtError.message : "unknown error",
    );
    return (
      <main className="page-shell vehicle-page-shell">
        <ErrorCard message="Extra dependencies could not be checked." />
      </main>
    );
  }
}

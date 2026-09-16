import Link from "next/link";
import { connection } from "next/server";
import { Suspense } from "react";
import RouteLoading from "@/components/app-shell/route-loading";
import { redirect } from "next/navigation";

import { hasLegacyRole } from "@/app/(administration)/drivers/access";
import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import { deleteMakeAction } from "@/app/(administration)/validation-data/makes/actions";
import { getMake, getMakeDeleteCheck, MakeApiError } from "@/lib/api/reference-data/api-makes";
import { getSession } from "@/lib/auth/session";

type MakeDeleteCheckPageProps = {
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
      <p className="eyebrow">Make deletion</p>
      <h2>{message}</h2>
      <Link className="button button-secondary" href="/Validation/MNT_make.aspx">
        Make Maintenance
      </Link>
    </section>
  );
}

async function renderMakeDeleteCheckPage({ searchParams }: MakeDeleteCheckPageProps) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired")
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath="/Validation/MNT_Make_Del_Check.aspx" />
      </main>
    );
  if (session.status === "unavailable")
    return (
      <main className="page-shell vehicle-page-shell">
        <ErrorCard message="Make deletion is temporarily unavailable." />
      </main>
    );
  if (!hasLegacyRole(session.roles, "Validation"))
    return (
      <main className="page-shell vehicle-page-shell">
        <ErrorCard message="You do not have permission to delete vehicle makes." />
      </main>
    );

  const query = await searchParams;
  const makeCode = parseCode(getQueryValue(query.code) ?? getQueryValue(query.makeCode));
  const error = getQueryValue(query.error);
  if (!makeCode)
    return (
      <main className="page-shell vehicle-page-shell">
        <ErrorCard message="Select a make before attempting deletion." />
      </main>
    );
  if (error)
    return (
      <main className="page-shell vehicle-page-shell">
        <ErrorCard message={error} />
      </main>
    );

  try {
    const [make, dependencies] = await Promise.all([
      getMake(makeCode),
      getMakeDeleteCheck(makeCode),
    ]);
    if (!make)
      return (
        <main className="page-shell vehicle-page-shell">
          <ErrorCard message={`Make ${makeCode} was not found.`} />
        </main>
      );
    const blocked = dependencies.modelCount > 0;
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-card" aria-labelledby="make-delete-title">
          <header className="vehicle-page-header">
            <div>
              <p className="eyebrow">Validation / Vehicle</p>
              <h1 id="make-delete-title">Delete Make</h1>
              <p>Check linked models before deleting this make.</p>
            </div>
            <Link className="button button-secondary" href="/Validation/MNT_make.aspx">
              Make Maintenance
            </Link>
          </header>
          <section className="vehicle-status-card" role={blocked ? "alert" : "note"}>
            <p className="eyebrow">Make {make.makeCode}</p>
            <h2>{make.makeDescription}</h2>
            <dl className="status-maintenance-details">
              <div>
                <dt>Linked models</dt>
                <dd>{dependencies.modelCount}</dd>
              </div>
            </dl>
            {blocked ? (
              <>
                <p className="muted-copy">
                  Please remove the linked models before deleting this make.
                </p>
                <Link className="button button-secondary" href="/Validation/MNT_make.aspx">
                  Return to Make Maintenance
                </Link>
              </>
            ) : (
              <form action={deleteMakeAction} className="button-row">
                <input name="makeCode" type="hidden" value={make.makeCode} readOnly />
                <button className="button button-primary" type="submit">
                  Confirm Delete
                </button>
                <Link className="button button-secondary" href="/Validation/MNT_make.aspx">
                  Cancel
                </Link>
              </form>
            )}
          </section>
        </section>
      </main>
    );
  } catch (error) {
    if (error instanceof MakeApiError && error.reason === "unauthorized")
      return (
        <main className="page-shell vehicle-page-shell">
          <SessionRecovery returnPath={`/Validation/MNT_Make_Del_Check.aspx?code=${makeCode}`} />
        </main>
      );
    if (error instanceof MakeApiError && error.status === 404)
      return (
        <main className="page-shell vehicle-page-shell">
          <ErrorCard message={`Make ${makeCode} was not found.`} />
        </main>
      );
    console.error(
      "FIS make delete check failed",
      error instanceof Error ? error.message : "unknown error",
    );
    return (
      <main className="page-shell vehicle-page-shell">
        <ErrorCard message="Make dependencies could not be checked." />
      </main>
    );
  }
}

export default function MakeDeleteCheckPage(props: MakeDeleteCheckPageProps) {
  return <Suspense fallback={<RouteLoading />}>{renderMakeDeleteCheckPage(props)}</Suspense>;
}

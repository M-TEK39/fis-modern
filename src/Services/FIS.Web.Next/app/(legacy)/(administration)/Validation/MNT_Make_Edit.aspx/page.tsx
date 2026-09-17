import Link from "next/link";
import { connection } from "next/server";
import { Suspense } from "react";
import RouteLoading from "@/components/app-shell/route-loading";
import { redirect } from "next/navigation";

import { hasLegacyRole } from "@/app/(administration)/drivers/access";
import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import { updateMakeAction } from "@/app/(administration)/validation-data/makes/actions";
import MakeForm from "@/app/(administration)/validation-data/makes/make-form";
import { MakeApiError, getMake } from "@/lib/api/reference-data/api-makes";
import { getSession } from "@/lib/auth/session";

type MakeEditPageProps = { searchParams: Promise<Record<string, string | string[] | undefined>> };

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
      <p className="eyebrow">Make maintenance</p>
      <h2>{message}</h2>
      <Link className="button button-secondary" href="/Validation/MNT_make.aspx">
        Make Maintenance
      </Link>
    </section>
  );
}

async function renderMakeEditPage({ searchParams }: MakeEditPageProps) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired")
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath="/Validation/MNT_Make_Edit.aspx" />
      </main>
    );
  if (session.status === "unavailable")
    return (
      <main className="page-shell vehicle-page-shell">
        <ErrorCard message="Make maintenance is temporarily unavailable." />
      </main>
    );
  if (!hasLegacyRole(session.roles, "Validation"))
    return (
      <main className="page-shell vehicle-page-shell">
        <ErrorCard message="You do not have permission to edit vehicle makes." />
      </main>
    );

  const query = await searchParams;
  const makeCode = parseCode(
    getQueryValue(query.cmbMake) ?? getQueryValue(query.makeCode) ?? getQueryValue(query.code),
  );
  if (!makeCode)
    return (
      <main className="page-shell vehicle-page-shell">
        <ErrorCard message="Select a make before opening edit." />
      </main>
    );

  try {
    const make = await getMake(makeCode);
    if (!make)
      return (
        <main className="page-shell vehicle-page-shell">
          <ErrorCard message={`Make ${makeCode} was not found.`} />
        </main>
      );
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-card" aria-labelledby="make-edit-title">
          <header className="vehicle-page-header">
            <div>
              <p className="eyebrow">Validation / Vehicle</p>
              <h1 id="make-edit-title">Edit Make</h1>
              <p>Update make {makeCode} without dropping its legacy identity.</p>
            </div>
            <Link className="button button-secondary" href="/Validation/MNT_make.aspx">
              Make Maintenance
            </Link>
          </header>
          <MakeForm action={updateMakeAction} make={make} mode="update" />
        </section>
      </main>
    );
  } catch (error) {
    if (error instanceof MakeApiError && error.reason === "unauthorized")
      return (
        <main className="page-shell vehicle-page-shell">
          <SessionRecovery returnPath={`/Validation/MNT_Make_Edit.aspx?cmbMake=${makeCode}`} />
        </main>
      );
    if (error instanceof MakeApiError && error.status === 404)
      return (
        <main className="page-shell vehicle-page-shell">
          <ErrorCard message={`Make ${makeCode} was not found.`} />
        </main>
      );
    console.error(
      "FIS make edit request failed",
      error instanceof Error ? error.message : "unknown error",
    );
    return (
      <main className="page-shell vehicle-page-shell">
        <ErrorCard message="Make details could not be loaded." />
      </main>
    );
  }
}

export default function MakeEditPage(props: MakeEditPageProps) {
  return <Suspense fallback={<RouteLoading />}>{renderMakeEditPage(props)}</Suspense>;
}

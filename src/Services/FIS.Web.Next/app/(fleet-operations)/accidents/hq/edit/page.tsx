import Link from "next/link";
import { notFound, redirect } from "next/navigation";
import { connection } from "next/server";
import { Suspense } from "react";

import { logoutAction } from "@/app/(auth)/actions/auth";
import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import HqAccidentForm from "@/app/(fleet-operations)/accidents/hq/hq-accident-form";
import {
  AccidentApiError,
  getAccidentForEdit,
  getAccidentReferenceData,
} from "@/lib/api/fleet-operations/api-accidents";
import { getSession } from "@/lib/auth/session";

const ACCIDENTS_ROLE = "Accidents";

type HqEditPageProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
};

function getQueryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

function getAccidentId(value: string | undefined) {
  const parsed = Number(value);
  return value && Number.isInteger(parsed) && parsed > 0 ? parsed : null;
}

function hasRole(roles: readonly string[], role: string) {
  return roles.some(
    (candidate) => candidate.localeCompare(role, undefined, { sensitivity: "accent" }) === 0,
  );
}

function ApiUnavailable() {
  return (
    <section className="vehicle-status-card" role="alert">
      <div className="status-icon status-icon-error" aria-hidden="true">
        !
      </div>
      <p className="eyebrow">API unavailable</p>
      <h2>The accident record could not be loaded.</h2>
      <p className="muted-copy">Retry when the FIS API is available.</p>
      <Link className="button button-primary" href="/accidents/hq">
        Return to search
      </Link>
    </section>
  );
}

function AccidentNotFound() {
  return (
    <section className="vehicle-status-card" role="alert">
      <p className="eyebrow">Accident not found</p>
      <h2>That accident record could not be found.</h2>
      <Link className="button button-secondary" href="/accidents/hq">
        Return to search
      </Link>
    </section>
  );
}

async function HqEditContent({ searchParams }: HqEditPageProps) {
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired") return <SessionRecovery returnPath="/accidents/hq/edit" />;
  if (session.status === "unavailable") return <ApiUnavailable />;
  if (!hasRole(session.roles, ACCIDENTS_ROLE)) {
    return (
      <section className="vehicle-status-card" role="alert">
        <p className="eyebrow">Access restricted</p>
        <h2>You do not have permission to edit HQ accidents.</h2>
      </section>
    );
  }

  const query = await searchParams;
  const accidentId = getAccidentId(getQueryValue(query.accidentId) ?? getQueryValue(query.Code));
  if (accidentId === null) notFound();

  try {
    const [accident, referenceData] = await Promise.all([
      getAccidentForEdit(accidentId),
      getAccidentReferenceData(),
    ]);
    return (
      <>
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Accident maintenance</p>
            <h1 id="hq-edit-title">Edit HQ accident #{accident.accidentCode}</h1>
            <p>Update the HQ accident record and its references.</p>
          </div>
          <Link className="button button-secondary" href="/accidents">
            Accident Menu
          </Link>
        </header>
        <HqAccidentForm mode="edit" accident={accident} sites={referenceData.sites} />
        <div className="vehicle-footer-actions">
          <Link className="button button-secondary" href="/accidents/hq">
            Back to Search
          </Link>
          <Link className="button button-secondary" href="/home">
            Home
          </Link>
          <form action={logoutAction}>
            <button className="button button-secondary" type="submit">
              Sign out
            </button>
          </form>
        </div>
      </>
    );
  } catch (error) {
    if (error instanceof AccidentApiError && error.reason === "unauthorized")
      return <SessionRecovery returnPath={`/accidents/hq/edit?accidentId=${accidentId}`} />;
    if (error instanceof AccidentApiError && error.reason === "not-found")
      return <AccidentNotFound />;
    console.error(
      "FIS HQ accident edit request failed",
      error instanceof Error ? error.message : "unknown error",
    );
    return <ApiUnavailable />;
  }
}

export default async function HqEditPage({ searchParams }: HqEditPageProps) {
  await connection();
  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-labelledby="hq-edit-title">
        <Suspense
          fallback={
            <div className="loading-card" aria-busy="true">
              <span className="spinner" aria-hidden="true" />
              <p>Loading accident details...</p>
            </div>
          }
        >
          <HqEditContent searchParams={searchParams} />
        </Suspense>
      </section>
    </main>
  );
}

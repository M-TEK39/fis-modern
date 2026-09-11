import Link from "next/link";
import { notFound, redirect } from "next/navigation";
import { connection } from "next/server";
import { Suspense } from "react";

import { logoutAction } from "@/app/(auth)/actions/auth";
import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import GarageDeleteConfirm from "@/app/(fleet-operations)/accidents/garage/delete/garage-delete-confirm";
import { AccidentApiError, getAccidentForEdit } from "@/lib/api/fleet-operations/api-accidents";
import { getSession } from "@/lib/auth/session";

const ACCIDENTS_ROLE = "Accidents";

type GarageDeleteConfirmPageProps = {
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
      <Link className="button button-primary" href="/accidents/garage/delete">
        Return to search
      </Link>
    </section>
  );
}

function AccidentNotFound() {
  return (
    <section className="vehicle-status-card" role="alert">
      <div className="status-icon status-icon-error" aria-hidden="true">
        !
      </div>
      <p className="eyebrow">Accident not found</p>
      <h2>That accident record could not be found.</h2>
      <Link className="button button-secondary" href="/accidents/garage/delete">
        Return to search
      </Link>
    </section>
  );
}

async function GarageDeleteConfirmContent({ searchParams }: GarageDeleteConfirmPageProps) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") {
    redirect("/login");
  }

  if (session.status === "expired") {
    return <SessionRecovery returnPath="/accidents/garage/delete" />;
  }

  if (session.status === "unavailable") {
    return <ApiUnavailable />;
  }

  if (!hasRole(session.roles, ACCIDENTS_ROLE)) {
    return (
      <section className="vehicle-status-card" role="alert">
        <div className="status-icon status-icon-error" aria-hidden="true">
          !
        </div>
        <p className="eyebrow">Access restricted</p>
        <h2>You do not have permission to delete garage accidents.</h2>
      </section>
    );
  }

  const query = await searchParams;
  const accidentId = getAccidentId(getQueryValue(query.accidentId) ?? getQueryValue(query.ACode));
  if (accidentId === null) {
    notFound();
  }

  try {
    const accident = await getAccidentForEdit(accidentId);
    return (
      <>
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Accident maintenance</p>
            <h1 id="garage-delete-confirm-title">Delete accident #{accident.accidentCode}</h1>
            <p>Review the supported record details before confirming deletion.</p>
          </div>
          <Link className="button button-secondary" href="/accidents/garage/delete">
            Back to Search
          </Link>
        </header>
        <GarageDeleteConfirm accident={accident} />
        <div className="vehicle-footer-actions">
          <Link className="button button-secondary" href="/accidents">
            Menu
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
    if (error instanceof AccidentApiError && error.reason === "unauthorized") {
      return (
        <SessionRecovery returnPath={`/accidents/garage/delete/confirm?accidentId=${accidentId}`} />
      );
    }

    if (error instanceof AccidentApiError && error.reason === "not-found") {
      return <AccidentNotFound />;
    }

    console.error(
      "FIS garage accident delete detail request failed",
      error instanceof Error ? error.message : "unknown error",
    );
    return <ApiUnavailable />;
  }
}

export default function GarageDeleteConfirmPage({ searchParams }: GarageDeleteConfirmPageProps) {
  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-labelledby="garage-delete-confirm-title">
        <Suspense
          fallback={
            <div className="loading-card" aria-busy="true">
              <span className="spinner" aria-hidden="true" />
              <p>Loading page…</p>
            </div>
          }
        >
          <GarageDeleteConfirmContent searchParams={searchParams} />
        </Suspense>
      </section>
    </main>
  );
}

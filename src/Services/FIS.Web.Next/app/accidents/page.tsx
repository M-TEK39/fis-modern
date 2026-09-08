import Link from "next/link";
import { Suspense, type ReactNode } from "react";
import { connection } from "next/server";
import { redirect } from "next/navigation";

import { logoutAction } from "@/app/actions/auth";
import SessionRecovery from "@/app/home/session-recovery";
import { getSession } from "@/lib/session";

const ACCIDENTS_ROLE = "Accidents";

function AccidentMenuLink({ href, children }: { href: string; children: ReactNode }) {
  return (
    <div className="vehicle-menu-item">
      <Link className="vehicle-menu-link" href={href}>
        {children}
      </Link>
    </div>
  );
}

function UnavailableState() {
  return (
    <section className="vehicle-status-card" role="alert">
      <div className="status-icon status-icon-error" aria-hidden="true">
        !
      </div>
      <p className="eyebrow">API unavailable</p>
      <h2>Your session could not be checked.</h2>
      <p className="muted-copy">
        The application is still running. Retry when the FIS API is available.
      </p>
      <div className="button-row">
        <Link className="button button-primary" href="/accidents">
          Try again
        </Link>
        <Link className="button button-secondary" href="/login">
          Sign in
        </Link>
      </div>
    </section>
  );
}

function AccessRestricted() {
  return (
    <section className="vehicle-status-card" role="alert">
      <div className="status-icon status-icon-error" aria-hidden="true">
        !
      </div>
      <p className="eyebrow">Access restricted</p>
      <h2>You do not have permission to access Accident Maintenance.</h2>
      <p className="muted-copy">
        Contact your FIS administrator if you need accident-management access.
      </p>
    </section>
  );
}

async function AccidentMenuContent() {
  const session = await getSession();

  if (session.status === "anonymous") {
    redirect("/login");
  }

  if (session.status === "expired") {
    return <SessionRecovery returnPath="/accidents" />;
  }

  if (session.status === "unavailable") {
    return <UnavailableState />;
  }

  if (
    !session.roles.some(
      (role) => role.localeCompare(ACCIDENTS_ROLE, undefined, { sensitivity: "accent" }) === 0,
    )
  ) {
    return <AccessRestricted />;
  }

  return (
    <>
      <div className="vehicle-menu-tiles">
        <section className="vehicle-menu-tile">
          <h2 className="sr-only">Accident help</h2>
          <details open>
            <summary className="vehicle-menu-header">
              Accident Maintenance Information / Help
            </summary>
            <div className="vehicle-menu-body">
              <AccidentMenuLink href="/accidents/help">
                Accident Maintenance Information / Help
              </AccidentMenuLink>
            </div>
          </details>
        </section>

        <section className="vehicle-menu-tile">
          <h2 className="sr-only">Garage accident maintenance</h2>
          <details open>
            <summary className="vehicle-menu-header">Accident Maintenance for Garage</summary>
            <div className="vehicle-menu-body">
              <AccidentMenuLink href="/accidents/garage">1) Accident Maintenance</AccidentMenuLink>
              <AccidentMenuLink href="/accidents/garage/delete">
                2) Delete an Accident
              </AccidentMenuLink>
            </div>
          </details>
        </section>

        <section className="vehicle-menu-tile">
          <h2 className="sr-only">HQ accident maintenance</h2>
          <details open>
            <summary className="vehicle-menu-header">Accident Maintenance for HQ</summary>
            <div className="vehicle-menu-body">
              <AccidentMenuLink href="/accidents/hq">3) Accident Maintenance</AccidentMenuLink>
              <AccidentMenuLink href="/accidents/hq/delete">4) Delete an Accident</AccidentMenuLink>
            </div>
          </details>
        </section>

        <section className="vehicle-menu-tile">
          <h2 className="sr-only">Accident reports</h2>
          <details open>
            <summary className="vehicle-menu-header">Accident Reports</summary>
            <div className="vehicle-menu-body">
              <AccidentMenuLink href="/accidents/reports">1) Accidents Reports</AccidentMenuLink>
            </div>
          </details>
        </section>
      </div>

      <div className="vehicle-footer-actions">
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
}

function AccidentMenuFallback() {
  return (
    <div className="loading-card" aria-busy="true">
      <span className="spinner" aria-hidden="true" />
      <p>Checking accident access...</p>
    </div>
  );
}

export default async function AccidentsPage() {
  await connection();

  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-labelledby="accident-menu-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Fleet operations</p>
            <h1 id="accident-menu-title">Accident Maintenance Menu</h1>
            <p>Accident maintenance workflows and reports.</p>
          </div>
        </header>
        <Suspense fallback={<AccidentMenuFallback />}>
          <AccidentMenuContent />
        </Suspense>
      </section>
    </main>
  );
}

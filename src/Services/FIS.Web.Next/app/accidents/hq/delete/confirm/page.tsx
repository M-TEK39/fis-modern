import Link from "next/link";
import { notFound, redirect } from "next/navigation";
import { connection } from "next/server";
import { Suspense } from "react";

import { logoutAction } from "@/app/actions/auth";
import SessionRecovery from "@/app/home/session-recovery";
import HqDeleteConfirm from "@/app/accidents/hq/delete/hq-delete-confirm";
import { AccidentApiError, getAccidentForEdit } from "@/lib/api-accidents";
import { getSession } from "@/lib/session";

const ACCIDENTS_ROLE = "Accidents";

type HqDeleteConfirmPageProps = {
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
  return roles.some((candidate) => candidate.localeCompare(role, undefined, { sensitivity: "accent" }) === 0);
}

function ErrorState({ notFoundState = false }: Readonly<{ notFoundState?: boolean }>) {
  return (
    <section className="vehicle-status-card" role="alert">
      <p className="eyebrow">{notFoundState ? "Accident not found" : "API unavailable"}</p>
      <h2>{notFoundState ? "That accident record could not be found." : "The accident record could not be loaded."}</h2>
      <Link className="button button-secondary" href="/accidents/hq/delete">Return to search</Link>
    </section>
  );
}

async function HqDeleteConfirmContent({ searchParams }: HqDeleteConfirmPageProps) {
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired") return <SessionRecovery returnPath="/accidents/hq/delete" />;
  if (session.status === "unavailable") return <ErrorState />;
  if (!hasRole(session.roles, ACCIDENTS_ROLE)) return <ErrorState />;

  const query = await searchParams;
  const accidentId = getAccidentId(getQueryValue(query.accidentId) ?? getQueryValue(query.ACode));
  if (accidentId === null) notFound();

  try {
    const accident = await getAccidentForEdit(accidentId);
    return (
      <>
        <header className="vehicle-page-header"><div><p className="eyebrow">Accident maintenance</p><h1 id="hq-delete-confirm-title">Delete HQ accident #{accident.accidentCode}</h1><p>Review the supported record details before confirming deletion.</p></div><Link className="button button-secondary" href="/accidents/hq/delete">Back to Search</Link></header>
        <HqDeleteConfirm accident={accident} />
        <div className="vehicle-footer-actions"><Link className="button button-secondary" href="/accidents">Menu</Link><Link className="button button-secondary" href="/home">Home</Link><form action={logoutAction}><button className="button button-secondary" type="submit">Sign out</button></form></div>
      </>
    );
  } catch (error) {
    if (error instanceof AccidentApiError && error.reason === "unauthorized") return <SessionRecovery returnPath={`/accidents/hq/delete/confirm?accidentId=${accidentId}`} />;
    if (error instanceof AccidentApiError && error.reason === "not-found") return <ErrorState notFoundState />;
    console.error("FIS HQ accident delete detail request failed", error instanceof Error ? error.message : "unknown error");
    return <ErrorState />;
  }
}

export default async function HqDeleteConfirmPage({ searchParams }: HqDeleteConfirmPageProps) {
  await connection();
  return <main className="page-shell vehicle-page-shell"><section className="vehicle-card" aria-labelledby="hq-delete-confirm-title"><Suspense fallback={<div className="loading-card" aria-busy="true"><span className="spinner" aria-hidden="true" /><p>Loading accident details...</p></div>}><HqDeleteConfirmContent searchParams={searchParams} /></Suspense></section></main>;
}

import DataTableHeader from "@/components/ui/data-table-header";

import Link from "next/link";
import { connection } from "next/server";
import { redirect } from "next/navigation";
import { Suspense } from "react";

import PrintButton from "@/app/(fleet-operations)/accidents/reports/print-button";
import { logoutAction } from "@/app/(auth)/actions/auth";
import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import {
  AccidentApiError,
  getAccidentTypes,
  type AccidentTypeOption,
} from "@/lib/api/fleet-operations/api-accidents";
import { getSession } from "@/lib/auth/session";

const ACCIDENTS_ROLE = "Accidents";

function hasRole(roles: readonly string[], role: string) {
  return roles.some(
    (candidate) => candidate.localeCompare(role, undefined, { sensitivity: "accent" }) === 0,
  );
}

function LoadingState() {
  return (
    <div className="loading-card" aria-busy="true">
      <span className="spinner" aria-hidden="true" />
      <p>Loading page…</p>
    </div>
  );
}

function ErrorState() {
  return (
    <section className="vehicle-status-card" role="alert">
      <p className="eyebrow">API unavailable</p>
      <h2>The accident category list could not be loaded.</h2>
      <p className="muted-copy">Retry when the FIS API is available.</p>
      <Link className="button button-primary" href="/accidents/reports/categories">
        Try again
      </Link>
    </section>
  );
}

function CategoryTable({ categories }: { categories: AccidentTypeOption[] }) {
  if (categories.length === 0) {
    return (
      <section className="vehicle-empty-state" aria-live="polite">
        <p className="eyebrow">No categories found</p>
        <h2>No accident categories are available.</h2>
      </section>
    );
  }

  return (
    <section
      className="vehicle-status-maintenance-panel"
      aria-labelledby="accident-category-results-title"
    >
      <div className="vehicle-form-section-header">
        <div>
          <p className="eyebrow">Reference data</p>
          <h2 id="accident-category-results-title">Accident categories</h2>
        </div>
        <span className="form-hint">
          {categories.length} categor{categories.length === 1 ? "y" : "ies"}
        </span>
      </div>
      <div className="vehicle-table-wrapper">
        <table className="vehicle-table">
          <caption className="sr-only">Accident category list</caption>
          <DataTableHeader
            columns={[
              { key: "column-1", label: <>Category Number</> },
              { key: "column-2", label: <>Accident Category Description</> },
            ]}
          />
          <tbody>
            {categories.map((category) => (
              <tr key={category.typeCode}>
                <td>{category.typeCode}</td>
                <td>{category.description}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  );
}

async function AccidentCategoriesContent() {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired")
    return <SessionRecovery returnPath="/accidents/reports/categories" />;
  if (session.status === "unavailable") return <ErrorState />;
  if (!hasRole(session.roles, ACCIDENTS_ROLE)) {
    return (
      <section className="vehicle-status-card" role="alert">
        <p className="eyebrow">Access restricted</p>
        <h2>You do not have permission to view accident categories.</h2>
      </section>
    );
  }

  try {
    return <CategoryTable categories={await getAccidentTypes()} />;
  } catch (error) {
    if (error instanceof AccidentApiError && error.reason === "unauthorized")
      return <SessionRecovery returnPath="/accidents/reports/categories" />;
    console.error(
      "FIS accident category lookup failed",
      error instanceof Error ? error.message : "unknown error",
    );
    return <ErrorState />;
  }
}

async function PrintedAt() {
  await connection();
  const printedAt = new Date();
  return <time dateTime={printedAt.toISOString()}>{printedAt.toLocaleString("en-ZA")}</time>;
}

export default function AccidentCategoriesPage() {
  return (
    <main className="page-shell vehicle-page-shell">
      <article className="vehicle-card" aria-labelledby="accident-categories-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Accident reports</p>
            <h1 id="accident-categories-title">Accident CATEGORY List</h1>
            <p>
              Date Printed:{" "}
              <Suspense fallback={<span aria-hidden="true">—</span>}>
                <PrintedAt />
              </Suspense>
            </p>
          </div>
          <div className="button-row">
            <Link className="button button-secondary" href="/accidents/reports">
              Report Menu
            </Link>
            <PrintButton label="Print category list" />
          </div>
        </header>
        <Suspense fallback={<LoadingState />}>
          <AccidentCategoriesContent />
        </Suspense>
        <div className="vehicle-footer-actions">
          <Link className="button button-secondary" href="/accidents">
            Accident Menu
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
      </article>
    </main>
  );
}

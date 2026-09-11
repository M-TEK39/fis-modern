import Link from "next/link";
import { connection } from "next/server";
import { redirect } from "next/navigation";
import { Suspense } from "react";

import { logoutAction } from "@/app/(auth)/actions/auth";
import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import {
  AccidentApiError,
  getAccidentPrivateVehicleReport,
  type AccidentPrivateVehicleReportMode,
  type AccidentVehicleReportRow,
} from "@/lib/api/fleet-operations/api-accidents";
import { getSession } from "@/lib/auth/session";
import VehicleReportResult from "@/app/(fleet-operations)/accidents/reports/vehicle-report-result";

const ACCIDENTS_ROLE = "Accidents";

type PrivateVehicleReportPageProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
  defaultMode?: AccidentPrivateVehicleReportMode;
};

function getQueryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

function getMode(
  value: string | undefined,
  defaultMode: AccidentPrivateVehicleReportMode,
): AccidentPrivateVehicleReportMode {
  return value === "description" ||
    value === "capture-description" ||
    value === "private-description"
    ? "description"
    : defaultMode;
}

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
      <h2>The private vehicle accident report could not be loaded.</h2>
      <p className="muted-copy">Retry when the FIS API is available.</p>
      <Link className="button button-primary" href="/accidents/reports/private-vehicle">
        Try again
      </Link>
    </section>
  );
}

function PrivateVehicleReportForm({
  descriptionMode,
  searchTerm,
}: {
  descriptionMode: boolean;
  searchTerm: string;
}) {
  return (
    <form className="vehicle-status-maintenance-panel" method="get">
      <div className="field">
        <label htmlFor="private-vehicle-report-search">Private Vehicle Number</label>
        <input
          id="private-vehicle-report-search"
          name="searchTerm"
          maxLength={8}
          defaultValue={searchTerm}
          required
        />
      </div>
      <input name="run" type="hidden" value="1" />
      <div className="button-row">
        <button className="button button-primary" type="submit">
          {descriptionMode ? "QUERY" : "SUBMIT"}
        </button>
        <Link className="button button-secondary" href="/accidents/reports">
          Report Menu
        </Link>
      </div>
    </form>
  );
}

function PrivateVehicleReportResults({
  rows,
  title,
}: {
  rows: AccidentVehicleReportRow[] | null;
  title: string;
}) {
  return rows !== null ? (
    rows.length === 0 ? (
      <div className="vehicle-empty-state">
        <p className="eyebrow">No vehicles found</p>
        <h2>No accidents matched this private vehicle search.</h2>
        <p className="muted-copy">Try another private vehicle number.</p>
      </div>
    ) : (
      <section aria-live="polite" aria-labelledby="private-vehicle-report-results-title">
        <div className="vehicle-form-section-header">
          <div>
            <p className="eyebrow">Report results</p>
            <h2 id="private-vehicle-report-results-title">
              {title}: {rows.length}
            </h2>
          </div>
        </div>
        {rows.map((row, index) => (
          <VehicleReportResult
            key={row.accidentCode}
            row={row}
            index={index}
            includeAccidentCategory={false}
          />
        ))}
      </section>
    )
  ) : null;
}

const PrivateVehicleReportContent = renderPrivateVehicleReportContent;

async function renderPrivateVehicleReportContent({
  searchParams,
  defaultMode,
}: Required<PrivateVehicleReportPageProps>) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired")
    return <SessionRecovery returnPath="/accidents/reports/private-vehicle" />;
  if (session.status === "unavailable") return <ErrorState />;
  if (!hasRole(session.roles, ACCIDENTS_ROLE)) {
    return (
      <section className="vehicle-status-card" role="alert">
        <p className="eyebrow">Access restricted</p>
        <h2>You do not have permission to run accident reports.</h2>
      </section>
    );
  }

  const query = await searchParams;
  const mode = getMode(getQueryValue(query.mode), defaultMode);
  const searchTerm = (getQueryValue(query.searchTerm) ?? getQueryValue(query.xnumber) ?? "").trim();
  const shouldRun = getQueryValue(query.run) === "1" || searchTerm.length > 0;
  let rows: AccidentVehicleReportRow[] | null = null;
  if (shouldRun && searchTerm) {
    try {
      rows = await getAccidentPrivateVehicleReport(searchTerm, mode);
    } catch (error) {
      if (error instanceof AccidentApiError && error.reason === "unauthorized")
        return <SessionRecovery returnPath="/accidents/reports/private-vehicle" />;
      console.error(
        "FIS private vehicle accident report failed",
        error instanceof Error ? error.message : "unknown error",
      );
      return <ErrorState />;
    }
  }

  const descriptionMode = mode === "description";
  const title = descriptionMode
    ? "Private Vehicle Accidents (Capture under Description of Accident)"
    : "Private Vehicle Accidents";
  const resultPath = descriptionMode
    ? "/accidents/reports/private-vehicle/description"
    : "/accidents/reports/private-vehicle";

  return (
    <>
      <PrivateVehicleReportForm descriptionMode={descriptionMode} searchTerm={searchTerm} />
      <PrivateVehicleReportResults rows={rows} title={title} />

      <div className="vehicle-footer-actions">
        <Link className="button button-secondary" href="/accidents">
          Accident Menu
        </Link>
        <Link className="button button-secondary" href={resultPath}>
          Clear
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
}

export function PrivateVehicleReportPage({
  searchParams,
  defaultMode = "third-party",
}: PrivateVehicleReportPageProps) {
  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-labelledby="private-vehicle-report-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Accident reports</p>
            <h1 id="private-vehicle-report-title">
              {defaultMode === "description"
                ? "Private Vehicle Accidents (Capture under Description of Accident)"
                : "Private Vehicle Accidents"}
            </h1>
            <p>
              Search accident details using the private vehicle number captured in the legacy
              workflow.
            </p>
          </div>
          <Link className="button button-secondary" href="/accidents/reports">
            Report Menu
          </Link>
        </header>
        <Suspense fallback={<LoadingState />}>
          <PrivateVehicleReportContent searchParams={searchParams} defaultMode={defaultMode} />
        </Suspense>
      </section>
    </main>
  );
}

export default function PrivateVehicleReportPageDefault({
  searchParams,
}: PrivateVehicleReportPageProps) {
  return <PrivateVehicleReportPage searchParams={searchParams} defaultMode="third-party" />;
}

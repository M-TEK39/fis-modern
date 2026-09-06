import Link from "next/link";
import { connection } from "next/server";
import { redirect } from "next/navigation";
import { Suspense } from "react";

import { logoutAction } from "@/app/actions/auth";
import SessionRecovery from "@/app/home/session-recovery";
import { createGgBlockAction } from "@/app/vehicles/gg-block-numbers/actions";
import GgBlockForm from "@/app/vehicles/gg-block-numbers/gg-block-form";
import { GgBlockApiError, getGgBlockHistory, type GgBlockHistoryPage } from "@/lib/api-gg-blocks";
import { getSession } from "@/lib/session";

const VEHICLE_MANAGEMENT_PERMISSION = 1;
const PAGE_SIZE = 12;

type GgBlockNumbersPageProps = {
  searchParams: Promise<{ page?: string | string[]; saved?: string | string[] }>;
  routePath?: "/vehicles/gg-block-numbers" | "/Master-File/Add_GGBlockNumbers.aspx";
};

function getQueryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

function hasVehicleManagementPermission(accessLevel?: string) {
  if (!accessLevel) {
    return false;
  }

  try {
    return (BigInt(accessLevel) & BigInt(VEHICLE_MANAGEMENT_PERMISSION)) === BigInt(VEHICLE_MANAGEMENT_PERMISSION);
  } catch {
    return false;
  }
}

function StatusCard({ title, message }: Readonly<{ title: string; message: string }>) {
  return (
    <section className="vehicle-status-card" role="alert">
      <div className="status-icon status-icon-error" aria-hidden="true">!</div>
      <p className="eyebrow">{title}</p>
      <h2>{message}</h2>
    </section>
  );
}

function formatDate(value: string | null) {
  if (!value) {
    return "-";
  }

  const date = new Date(value);
  return Number.isNaN(date.getTime())
    ? value
    : date.toLocaleString("en-ZA", { dateStyle: "short", timeStyle: "medium" });
}

function HistoryTable({ history }: Readonly<{ history: GgBlockHistoryPage }>) {
  return history.items.length === 0 ? (
    <div className="vehicle-empty-state">
      <p className="eyebrow">No history records found</p>
      <p>There are no GG block number ranges to display.</p>
    </div>
  ) : (
    <div className="table-container fis-mt-1">
      <div className="table-header"><span className="table-title">GG Block Number History...</span></div>
      <div className="table-wrapper">
        <table className="data-table">
          <thead>
            <tr>
              <th scope="col">Captured By</th>
              <th scope="col">Date Created</th>
              <th scope="col">Start GG Number</th>
              <th scope="col">End GG Number</th>
            </tr>
          </thead>
          <tbody>
            {history.items.map((row) => (
              <tr key={`${row.blockId}-${row.startGgNumber}-${row.endGgNumber}`}>
                <td>{row.capturedBy}</td>
                <td>{formatDate(row.dateCreated)}</td>
                <td>{row.startGgNumber}</td>
                <td>{row.endGgNumber}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}

function Pagination({ history, routePath }: Readonly<{ history: GgBlockHistoryPage; routePath: string }>) {
  if (history.totalPages <= 1) {
    return null;
  }

  const pageHref = (page: number) => page === 1 ? routePath : `${routePath}?page=${page}`;
  return (
    <nav className="pagination-controls" aria-label="GG block number history pagination">
      {history.page <= 1 ? <span className="button button-secondary" aria-disabled="true">Previous</span> : <Link className="button button-secondary" href={pageHref(history.page - 1)}>Previous</Link>}
      <span aria-live="polite">Page {history.page} of {history.totalPages}</span>
      {history.page >= history.totalPages ? <span className="button button-secondary" aria-disabled="true">Next</span> : <Link className="button button-secondary" href={pageHref(history.page + 1)}>Next</Link>}
    </nav>
  );
}

async function GgBlockHistoryContent({ page, routePath }: Readonly<{ page: number; routePath: string }>) {
  let history: GgBlockHistoryPage;
  try {
    history = await getGgBlockHistory(page, PAGE_SIZE);
  } catch (error) {
    if (error instanceof GgBlockApiError && error.reason === "unauthorized") {
      return <SessionRecovery returnPath={routePath} />;
    }

    console.error("FIS GG block history request failed", error instanceof Error ? error.message : "unknown error");
    return (
      <div className="vehicle-status-card" role="alert">
        <p className="eyebrow">API unavailable</p>
        <h2>GG block number history could not be loaded.</h2>
        <p className="muted-copy">Retry when the FIS API is available.</p>
        <Link className="button button-primary" href={routePath}>Try again</Link>
      </div>
    );
  }

  return (
    <>
      <HistoryTable history={history} />
      <Pagination history={history} routePath={routePath} />
    </>
  );
}

export default async function GgBlockNumbersPage({ searchParams, routePath = "/vehicles/gg-block-numbers" }: GgBlockNumbersPageProps) {
  await connection();
  const session = await getSession();

  if (session.status === "anonymous") {
    redirect("/login");
  }

  if (session.status === "expired") {
    return <main className="page-shell vehicle-page-shell"><SessionRecovery returnPath={routePath} /></main>;
  }

  if (session.status === "unavailable") {
    return <main className="page-shell vehicle-page-shell"><StatusCard title="API unavailable" message="The GG block service is unavailable." /></main>;
  }

  if (!hasVehicleManagementPermission(session.accessLevel)) {
    return <main className="page-shell vehicle-page-shell"><StatusCard title="Access restricted" message="You do not have permission to maintain GG block numbers." /></main>;
  }

  const query = await searchParams;
  const requestedPage = Number.parseInt(getQueryValue(query.page) ?? "1", 10);
  const page = Number.isFinite(requestedPage) ? Math.max(1, requestedPage) : 1;
  const saved = getQueryValue(query.saved) === "1";

  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-labelledby="gg-block-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Vehicle master maintenance</p>
            <h1 id="gg-block-title">GG Block Number Maintenance</h1>
            <p>Capture and review GG block number ranges.</p>
          </div>
          <Link className="button button-secondary" href="/vehicles">&lt; Previous Menu</Link>
        </header>

        {saved ? <div className="notice notice-success" role="status"><span aria-hidden="true">✓</span><span>GG block number range saved successfully.</span></div> : null}

        <GgBlockForm action={createGgBlockAction} returnPath={routePath} />

        <Suspense fallback={<div className="loading-card" aria-busy="true"><span className="spinner" aria-hidden="true" /><p>Loading GG block history...</p></div>}>
          <GgBlockHistoryContent page={page} routePath={routePath} />
        </Suspense>

        <div className="vehicle-footer-actions">
          <Link className="button button-secondary" href="/vehicles">Back to Vehicle Master</Link>
          <form action={logoutAction}><button className="button button-secondary" type="submit">Sign out</button></form>
        </div>
      </section>
    </main>
  );
}

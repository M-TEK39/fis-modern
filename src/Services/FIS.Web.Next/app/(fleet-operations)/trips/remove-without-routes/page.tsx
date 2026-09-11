import { Suspense } from "react";

import RouteLoading from "@/components/app-shell/route-loading";

import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import ConfirmSubmitButton from "@/app/(fleet-operations)/trips/remove-without-routes/confirm-submit-button";
import { removeTripsWithoutRoutesAction } from "@/app/(fleet-operations)/trips/remove-without-routes/actions";
import {
  hasTripAuthorityAccess,
  getTripSession,
  tripAccessRestricted,
  tripSessionMessage,
} from "@/app/(fleet-operations)/trips/_page";
import {
  TripToolsApiError,
  getTripsWithoutRoutesPage,
  DEFAULT_TRIPS_WITHOUT_ROUTES_PAGE_SIZE,
  type TripsWithoutRoutesPage,
} from "@/lib/api/fleet-operations/api-trip-tools";

type SearchParams = Promise<Record<string, string | string[] | undefined>>;

function queryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

function formatDate(value: string | null) {
  return value?.slice(0, 10) || "-";
}

function valueOrDash(value: string | number | null | undefined) {
  return value === null || value === undefined || String(value).trim() === "" ? "-" : String(value);
}

function resultMessage(result: string | undefined, removed: string | undefined) {
  if (result === "success")
    return { tone: "success", text: `${removed ?? "0"} trip(s) without routes removed.` } as const;
  if (result === "forbidden")
    return {
      tone: "error",
      text: "You do not have permission to remove trips without routes.",
    } as const;
  if (result === "unauthorized")
    return { tone: "error", text: "Your session is no longer authorized. Sign in again." } as const;
  if (result === "unavailable")
    return {
      tone: "error",
      text: "The trip cleanup service is unavailable. Retry when the API is available.",
    } as const;
  if (result === "error")
    return { tone: "error", text: "The trips without routes could not be removed." } as const;
  return null;
}

function TripsTable({ result }: Readonly<{ result: TripsWithoutRoutesPage }>) {
  const { items: rows } = result;
  return (
    <section
      className="vehicle-status-maintenance-panel"
      aria-labelledby="trips-without-routes-results-title"
    >
      <div className="vehicle-form-section-header">
        <div>
          <p className="eyebrow">
            {result.total} record{result.total === 1 ? "" : "s"}
          </p>
          <h2 id="trips-without-routes-results-title">Report Results</h2>
        </div>
      </div>
      {rows.length === 0 ? (
        <div className="vehicle-empty-state">
          <p>No Records Found!</p>
        </div>
      ) : (
        <div className="vehicle-table-wrapper">
          <table className="vehicle-table">
            <caption className="sr-only">Trips without routes available for removal</caption>
            <thead>
              <tr>
                <th scope="col">Trip Authority</th>
                <th scope="col">Contract</th>
                <th scope="col">Issue Date</th>
                <th scope="col">Trip Request</th>
                <th scope="col">Trip Reason</th>
                <th scope="col">Approver</th>
              </tr>
            </thead>
            <tbody>
              {rows.map((row, index) => (
                <tr key={`${row.tripAuthorityCode ?? "trip"}-${index}`}>
                  <td>{valueOrDash(row.tripAuthorityCode)}</td>
                  <td>{valueOrDash(row.contractCode)}</td>
                  <td>{formatDate(row.issueDate)}</td>
                  <td>{valueOrDash(row.tripRequestNumber)}</td>
                  <td>{valueOrDash(row.tripReason)}</td>
                  <td>{valueOrDash(row.approverName)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  );
}

function TripsPagination({ page }: Readonly<{ page: TripsWithoutRoutesPage }>) {
  if (page.totalPages <= 1) return null;

  const href = (nextPage: number) =>
    `/trips/remove-without-routes?${new URLSearchParams({ page: String(nextPage) }).toString()}`;

  return (
    <nav className="table-pagination" aria-label="Trips without routes pages">
      {page.page > 1 ? (
        <Link className="button button-secondary button-small" href={href(page.page - 1)}>
          Previous
        </Link>
      ) : (
        <span className="button button-secondary button-small" aria-disabled="true">
          Previous
        </span>
      )}
      <span aria-live="polite">
        Page {page.page} of {page.totalPages}
      </span>
      {page.page < page.totalPages ? (
        <Link className="button button-secondary button-small" href={href(page.page + 1)}>
          Next
        </Link>
      ) : (
        <span className="button button-secondary button-small" aria-disabled="true">
          Next
        </span>
      )}
    </nav>
  );
}

function ApiUnavailable() {
  return (
    <section className="vehicle-status-card" role="alert">
      <p className="eyebrow">API unavailable</p>
      <h2>Trips without routes could not be loaded.</h2>
      <p className="muted-copy">Retry when the FIS API is available.</p>
      <Link className="button button-primary" href="/trips/remove-without-routes">
        Try again
      </Link>
    </section>
  );
}

async function RemoveTripsWithoutRoutesPageContent({
  searchParams,
}: Readonly<{ searchParams: SearchParams }>) {
  await connection();
  const session = await getTripSession();
  const sessionProblem = tripSessionMessage(session, "/trips/remove-without-routes");
  if (sessionProblem) return sessionProblem;
  if (session.status !== "authenticated")
    return tripAccessRestricted("Your session could not be loaded.");
  if (!hasTripAuthorityAccess(session)) return tripAccessRestricted();

  const query = await searchParams;
  const rawPage = Number(queryValue(query.page));
  const requestedPage = Number.isSafeInteger(rawPage) && rawPage > 0 ? rawPage : 1;
  let tripPage: TripsWithoutRoutesPage;
  try {
    tripPage = await getTripsWithoutRoutesPage({
      page: requestedPage,
      pageSize: DEFAULT_TRIPS_WITHOUT_ROUTES_PAGE_SIZE,
    });
  } catch (error) {
    if (error instanceof TripToolsApiError && error.reason === "unauthorized")
      return (
        <main className="page-shell vehicle-page-shell">
          <SessionRecovery returnPath="/trips/remove-without-routes" />
        </main>
      );
    console.error(
      "FIS trips-without-routes request failed",
      error instanceof Error ? error.message : "unknown error",
    );
    return (
      <main className="page-shell vehicle-page-shell">
        <ApiUnavailable />
      </main>
    );
  }

  const message = resultMessage(queryValue(query.result), queryValue(query.removed));
  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-labelledby="trips-without-routes-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Trip tools</p>
            <h1 id="trips-without-routes-title">Remove All Trips that have No routes</h1>
            <p>Review and remove trip authorities that were created without route records.</p>
          </div>
          <Link className="button button-secondary" href="/trip-authorities">
            Back to Trips
          </Link>
        </header>
        {message ? (
          <div
            className={`notice notice-${message.tone}`}
            role={message.tone === "error" ? "alert" : "status"}
          >
            {message.text}
          </div>
        ) : null}
        <TripsTable result={tripPage} />
        <TripsPagination page={tripPage} />
        {tripPage.total > 0 ? (
          <div className="button-row">
            <form action={removeTripsWithoutRoutesAction}>
              <ConfirmSubmitButton
                label="DELETE all Trips without routes"
                pendingLabel="Deleting..."
              />
            </form>
            <Link className="button button-secondary" href="/trip-authorities">
              Back
            </Link>
          </div>
        ) : (
          <div className="button-row">
            <Link className="button button-secondary" href="/trip-authorities">
              Back
            </Link>
          </div>
        )}
        <div className="vehicle-footer-actions">
          <Link className="button button-secondary" href="/home">
            Home
          </Link>
        </div>
      </section>
    </main>
  );
}

export default function RemoveTripsWithoutRoutesPage(
  props: Parameters<typeof RemoveTripsWithoutRoutesPageContent>[0],
) {
  return (
    <Suspense fallback={<RouteLoading />}>
      <RemoveTripsWithoutRoutesPageContent {...props} />
    </Suspense>
  );
}

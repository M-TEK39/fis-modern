import DataTableHeader from "@/components/ui/data-table-header";

import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import { StreamedRoute } from "@/components/app-shell/streamed-route";
import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import { getSession } from "@/lib/auth/session";
import {
  getTripsWithoutRoutesPage,
  TroubleshootApiError,
  DEFAULT_TROUBLESHOOT_PAGE_SIZE,
} from "@/lib/api/fleet-operations/api-troubleshoot";
import {
  StatusCard,
  TroubleshootMenu,
  TroubleshootShell,
} from "@/app/(fleet-operations)/troubleshoot/_components";
import {
  dateValue,
  hasTroubleshootingRole,
  valueOrDash,
} from "@/app/(fleet-operations)/troubleshoot/_utils";
import { removeTripsWithoutRoutesAction } from "@/app/(fleet-operations)/troubleshoot/actions";

type SearchParams = Promise<Record<string, string | string[] | undefined>>;

function first(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}
const RemoveTripsWithoutRoutesPageContent = renderRemoveTripsWithoutRoutesPageContent;

async function renderRemoveTripsWithoutRoutesPageContent({
  searchParams,
}: Readonly<{ searchParams: SearchParams }>) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired")
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath="/troubleshoot/remove-trips-no-routes" />
      </main>
    );
  if (session.status === "unavailable")
    return (
      <main className="page-shell vehicle-page-shell">
        <StatusCard
          title="API unavailable"
          message="The sign-in service is temporarily unavailable."
          href="/troubleshoot/remove-trips-no-routes"
        />
      </main>
    );
  if (!hasTroubleshootingRole(session.roles))
    return (
      <main className="page-shell vehicle-page-shell">
        <StatusCard
          title="Access restricted"
          message="You do not have permission to access Troubleshoot."
          href="/home"
        />
      </main>
    );

  const query = await searchParams;
  const rawPage = Number(first(query.page));
  const requestedPage = Number.isSafeInteger(rawPage) && rawPage > 0 ? rawPage : 1;
  let trips = {
    items: [],
    page: requestedPage,
    pageSize: DEFAULT_TROUBLESHOOT_PAGE_SIZE,
    total: 0,
    totalPages: 1,
  } as Awaited<ReturnType<typeof getTripsWithoutRoutesPage>>;
  let loadError: string | null = null;
  try {
    trips = await getTripsWithoutRoutesPage({
      page: requestedPage,
      pageSize: DEFAULT_TROUBLESHOOT_PAGE_SIZE,
    });
  } catch (error) {
    loadError =
      error instanceof TroubleshootApiError
        ? error.message
        : "Trips without routes could not be loaded.";
  }
  const saved = first(query.saved);

  return (
    <TroubleshootShell
      title="Remove Trips that have No routes"
      description="Review trips without routes before removing them."
    >
      <TroubleshootMenu />
      <section className="vehicle-status-maintenance-panel" aria-labelledby="remove-trips-title">
        <div className="vehicle-form-section-header">
          <div>
            <p className="eyebrow">
              {trips.total} candidate{trips.total === 1 ? "" : "s"}
            </p>
            <h2 id="remove-trips-title">Trips without routes</h2>
          </div>
        </div>
        <p className="muted-copy">
          The legacy action removes all candidates returned by the configured workflow. Dates can be
          supplied when the compatibility table supports them.
        </p>
        {loadError ? (
          <div className="notice notice-error" role="alert">
            {loadError}
          </div>
        ) : trips.items.length === 0 ? (
          <div className="vehicle-empty-state">
            <p>No trips without routes were found.</p>
          </div>
        ) : (
          <div className="vehicle-table-wrapper">
            <table className="vehicle-table">
              <caption className="sr-only">Trips without routes</caption>
              <DataTableHeader
                columns={[
                  { key: "column-1", label: <>Trip Authority</> },
                  { key: "column-2", label: <>Contract</> },
                  { key: "column-3", label: <>Issue date</> },
                  { key: "column-4", label: <>Trip reason</> },
                  { key: "column-5", label: <>Request number</> },
                  { key: "column-6", label: <>Approver</> },
                ]}
              />
              <tbody>
                {trips.items.map((trip) => (
                  <tr
                    key={
                      trip.tripAuthorityCode ??
                      [
                        trip.contractCode,
                        trip.issueDate,
                        trip.tripRequestNumber,
                        trip.approverName,
                      ].join("|")
                    }
                  >
                    <td>{valueOrDash(trip.tripAuthorityCode)}</td>
                    <td>{valueOrDash(trip.contractCode)}</td>
                    <td>{dateValue(trip.issueDate)}</td>
                    <td>{valueOrDash(trip.tripReason)}</td>
                    <td>{valueOrDash(trip.tripRequestNumber)}</td>
                    <td>{valueOrDash(trip.approverName)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
        {trips.totalPages > 1 ? (
          <nav className="table-pagination" aria-label="Trips without routes pages">
            {trips.page > 1 ? (
              <Link
                className="button button-secondary button-small"
                href={`/troubleshoot/remove-trips-no-routes?${new URLSearchParams({ page: String(trips.page - 1) }).toString()}`}
              >
                Previous
              </Link>
            ) : (
              <span className="button button-secondary button-small" aria-disabled="true">
                Previous
              </span>
            )}
            <span aria-live="polite">
              Page {trips.page} of {trips.totalPages}
            </span>
            {trips.page < trips.totalPages ? (
              <Link
                className="button button-secondary button-small"
                href={`/troubleshoot/remove-trips-no-routes?${new URLSearchParams({ page: String(trips.page + 1) }).toString()}`}
              >
                Next
              </Link>
            ) : (
              <span className="button button-secondary button-small" aria-disabled="true">
                Next
              </span>
            )}
          </nav>
        ) : null}
      </section>
      {saved ? (
        <div className="notice notice-success" role="status">
          Remove trips request submitted
          {saved === "0"
            ? "; no rows were reported as removed."
            : `; ${saved} row${saved === "1" ? "" : "s"} removed.`}
        </div>
      ) : null}
      <section
        className="vehicle-status-maintenance-panel"
        aria-labelledby="remove-trips-action-title"
      >
        <div className="vehicle-form-section-header">
          <div>
            <p className="eyebrow">Destructive action</p>
            <h2 id="remove-trips-action-title">Remove all trips without routes</h2>
          </div>
        </div>
        <form action={removeTripsWithoutRoutesAction} className="vehicle-create-form">
          <div className="form-grid">
            <div className="form-field">
              <label className="form-label" htmlFor="remove-trips-from">
                From
              </label>
              <input className="form-input" id="remove-trips-from" name="fromDate" type="date" />
            </div>
            <div className="form-field">
              <label className="form-label" htmlFor="remove-trips-to">
                To
              </label>
              <input className="form-input" id="remove-trips-to" name="toDate" type="date" />
            </div>
          </div>
          <label className="form-checkbox" htmlFor="remove-trips-confirm">
            <input id="remove-trips-confirm" name="confirm" value="yes" type="checkbox" required />{" "}
            I understand that this removes the matching trips without routes.
          </label>
          <div className="button-row">
            <button className="button button-danger" type="submit">
              Remove Trips
            </button>
          </div>
        </form>
      </section>
    </TroubleshootShell>
  );
}

export default function RemoveTripsWithoutRoutesPage(
  props: Readonly<{ searchParams: SearchParams }>,
) {
  return (
    <StreamedRoute>
      <RemoveTripsWithoutRoutesPageContent {...props} />
    </StreamedRoute>
  );
}

import DataTableHeader from "@/components/ui/data-table-header";

import { redirect } from "next/navigation";
import Link from "next/link";
import { connection } from "next/server";

import { StreamedRoute } from "@/components/app-shell/streamed-route";
import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import { getSession } from "@/lib/auth/session";
import {
  DEFAULT_TROUBLESHOOT_PAGE_SIZE,
  searchTroubleshootOdometerPage,
  TroubleshootApiError,
} from "@/lib/api/fleet-operations/api-troubleshoot";
import {
  Pagination,
  StatusCard,
  TroubleshootMenu,
  TroubleshootShell,
} from "@/app/(fleet-operations)/troubleshoot/_components";
import {
  hasTroubleshootingRole,
  pageNumber,
  valueOrDash,
} from "@/app/(fleet-operations)/troubleshoot/_utils";

type SearchParams = Promise<Record<string, string | string[] | undefined>>;
type SearchMode = "GG" | "REG" | "TA";

function first(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}
function modeValue(value: string | undefined): SearchMode {
  return value === "REG" || value === "TA" ? value : "GG";
}

const OdometerCorrectionsPageContent = renderOdometerCorrectionsPageContent;

async function renderOdometerCorrectionsPageContent({
  searchParams,
}: Readonly<{ searchParams: SearchParams }>) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired")
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath="/troubleshoot/odometer-corrections" />
      </main>
    );
  if (session.status === "unavailable")
    return (
      <main className="page-shell vehicle-page-shell">
        <StatusCard
          title="API unavailable"
          message="The sign-in service is temporarily unavailable."
          href="/troubleshoot/odometer-corrections"
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
  const mode = modeValue(first(query.mode));
  const searchValue = (first(query.value) ?? "").trim();
  const submitted = first(query.submitted) === "1";
  const page = pageNumber(query.page);
  let pageData: Awaited<ReturnType<typeof searchTroubleshootOdometerPage>> | null = null;
  let errorMessage: string | null = null;
  if (submitted) {
    if (!searchValue)
      errorMessage = "Please enter a GG/Registration Number or Trip Authority Number.";
    else if (mode === "TA" && !/^\d+$/.test(searchValue))
      errorMessage = "Please enter a valid Trip Authority Number.";
    else {
      try {
        pageData = await searchTroubleshootOdometerPage({
          searchMode: mode,
          searchValue,
          page,
          pageSize: DEFAULT_TROUBLESHOOT_PAGE_SIZE,
        });
      } catch (error) {
        errorMessage =
          error instanceof TroubleshootApiError
            ? error.message
            : "Odometer corrections could not be loaded.";
      }
    }
  }
  return (
    <TroubleshootShell
      title="ODOMeter Corrections"
      description="Search and review vehicle odometer corrections."
    >
      <TroubleshootMenu />
      <section className="vehicle-status-maintenance-panel" aria-labelledby="odometer-search-title">
        <div className="vehicle-form-section-header">
          <div>
            <p className="eyebrow">Search criteria</p>
            <h2 id="odometer-search-title">Find vehicle or trip authority</h2>
          </div>
        </div>
        <form className="vehicle-create-form" method="get">
          <input type="hidden" name="submitted" value="1" />
          <input type="hidden" name="page" value="1" />
          <div className="form-grid">
            <div className="form-field">
              <label className="form-label" htmlFor="odometer-mode">
                Search criteria
              </label>
              <select className="form-select" id="odometer-mode" name="mode" defaultValue={mode}>
                <option value="GG">Vehicle GG Number</option>
                <option value="REG">Vehicle Registration</option>
                <option value="TA">Trip Authority No.</option>
              </select>
            </div>
            <div className="form-field">
              <label className="form-label" htmlFor="odometer-value">
                Value
              </label>
              <input
                className="form-input"
                id="odometer-value"
                name="value"
                defaultValue={searchValue}
                required
              />
            </div>
          </div>
          <div className="button-row">
            <button className="button button-primary" type="submit">
              Submit Details
            </button>
            <Link className="button button-secondary" href="/troubleshoot/odometer-corrections">
              Clear
            </Link>
          </div>
        </form>
      </section>
      {errorMessage ? (
        <div className="notice notice-error" role="alert">
          {errorMessage}
        </div>
      ) : null}
      {!submitted ? (
        <div className="vehicle-empty-state">
          <p>Provide search criteria to locate odometer corrections.</p>
        </div>
      ) : errorMessage ? null : pageData?.total === 0 ? (
        <div className="vehicle-empty-state">
          <p>No results found.</p>
        </div>
      ) : (
        <section
          className="vehicle-status-maintenance-panel"
          aria-labelledby="odometer-results-title"
        >
          <div className="vehicle-form-section-header">
            <div>
              <p className="eyebrow">
                {pageData?.total ?? 0} result{pageData?.total === 1 ? "" : "s"}
              </p>
              <h2 id="odometer-results-title">Odometer results</h2>
            </div>
          </div>
          <div className="vehicle-table-wrapper">
            <table className="vehicle-table">
              <caption className="sr-only">Odometer correction results</caption>
              <DataTableHeader
                columns={[
                  { key: "column-1", label: <>Vehicle</> },
                  { key: "column-2", label: <>Trip Authority</> },
                  { key: "column-3", label: <>Current Odometer</> },
                  { key: "column-4", label: <>Last Odometer</> },
                ]}
              />
              <tbody>
                {pageData?.items.map((row) => (
                  <tr
                    key={[
                      row.vehicleIdentifier,
                      row.tripAuthorityNumber,
                      row.currentOdometer,
                      row.lastOdometer,
                    ].join("|")}
                  >
                    <td>{valueOrDash(row.vehicleIdentifier)}</td>
                    <td>{valueOrDash(row.tripAuthorityNumber)}</td>
                    <td>{valueOrDash(row.currentOdometer)}</td>
                    <td>{valueOrDash(row.lastOdometer)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <Pagination
            path="/troubleshoot/odometer-corrections"
            page={pageData?.page ?? page}
            totalPages={pageData?.totalPages ?? 1}
            query={{ submitted: 1, mode, value: searchValue }}
          />
        </section>
      )}
    </TroubleshootShell>
  );
}

export default function OdometerCorrectionsPage(props: Readonly<{ searchParams: SearchParams }>) {
  return (
    <StreamedRoute>
      <OdometerCorrectionsPageContent {...props} />
    </StreamedRoute>
  );
}

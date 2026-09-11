import DataTableHeader from "@/components/ui/data-table-header";

import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import { StreamedRoute } from "@/components/app-shell/streamed-route";
import {
  LossApiError,
  DEFAULT_LOSS_PAGE_SIZE,
  getLossesPage,
  type LossPage,
} from "@/lib/api/fleet-operations/api-losses";
import { getSession } from "@/lib/auth/session";

type SearchParams = Promise<Record<string, string | string[] | undefined>>;

export type LossMaintenancePageProps = { searchParams: SearchParams; routePath?: string };

function queryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

function valueOrDash(value: string | number | null | undefined) {
  return value === null || value === undefined || String(value).trim() === "" ? "-" : String(value);
}

function ApiUnavailable({ routePath }: Readonly<{ routePath: string }>) {
  return (
    <section className="vehicle-status-card" role="alert">
      <p className="eyebrow">API unavailable</p>
      <h2>Vehicle losses could not be loaded.</h2>
      <p className="muted-copy">
        The application is still running. Retry when the FIS API is available.
      </p>
      <Link className="button button-primary" href={routePath}>
        Try again
      </Link>
    </section>
  );
}

function resultsPageHref(routePath: string, identifier: string, mode: "GG" | "GP", page: number) {
  const params = new URLSearchParams({ identifier, mode });
  if (page > 1) params.set("page", String(page));
  return `${routePath}?${params.toString()}`;
}

function Results({
  lossPage,
  routePath,
  identifier,
  mode,
}: Readonly<{
  lossPage: LossPage;
  routePath: string;
  identifier: string;
  mode: "GG" | "GP";
}>) {
  const { items: losses, vmfCode } = lossPage;
  return (
    <section className="vehicle-status-maintenance-panel" aria-labelledby="loss-results-title">
      <div className="vehicle-form-section-header">
        <div>
          <p className="eyebrow">Sorted by loss date</p>
          <h2 id="loss-results-title">Vehicle losses</h2>
        </div>
      </div>
      {losses.length === 0 ? (
        <p className="muted-copy">No previous losses found for this vehicle.</p>
      ) : (
        <div className="vehicle-table-wrapper">
          <table className="vehicle-table">
            <caption className="sr-only">Vehicle loss records sorted by loss date</caption>
            <DataTableHeader
              columns={[
                { key: "column-1", label: <>Loss Date</> },
                { key: "column-2", label: <>Reference</> },
                { key: "column-3", label: <>Description</> },
                { key: "column-4", label: <>Amount</> },
                { key: "column-5", label: <>Department Claim</> },
                { key: "column-6", label: <>SAPD</> },
                { key: "column-7", label: <>Remarks</> },
                { key: "column-8", label: <>HQ Reference</> },
                { key: "column-9", label: <>Actions</> },
              ]}
            />
            <tbody>
              {losses.map((loss) => (
                <tr key={loss.lossCode}>
                  <td>{valueOrDash(loss.lossDate?.slice(0, 10))}</td>
                  <td>{valueOrDash(loss.lossReference)}</td>
                  <td>{valueOrDash(loss.lossTypeDescription ?? loss.lossTypeCode)}</td>
                  <td>{loss.lossAmount === null ? "-" : loss.lossAmount.toFixed(2)}</td>
                  <td>{loss.departmentClaim === null ? "-" : loss.departmentClaim.toFixed(2)}</td>
                  <td>{valueOrDash(loss.sapd)}</td>
                  <td>{valueOrDash(loss.remarks)}</td>
                  <td>{valueOrDash(loss.hqReference)}</td>
                  <td>
                    <div className="button-row">
                      <Link
                        className="button button-secondary button-small"
                        href={`/Losses/MNT_Losses_Edit.aspx?loss_code=${loss.lossCode}`}
                      >
                        Edit
                      </Link>
                      <Link
                        className="button button-danger button-small"
                        href={`/Losses/MNT_Losses_Delete.aspx?loss_code=${loss.lossCode}`}
                      >
                        Delete
                      </Link>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      {vmfCode ? (
        <Link
          className="button button-primary"
          href={`/Losses/MNT_Losses_Add.aspx?vmfCode=${vmfCode}`}
        >
          Add Losses
        </Link>
      ) : null}
      {lossPage.totalPages > 1 ? (
        <nav className="vehicle-pagination" aria-label="Vehicle loss pages">
          {lossPage.page <= 1 ? (
            <span
              className="vehicle-pagination-button vehicle-pagination-disabled"
              aria-disabled="true"
            >
              Previous
            </span>
          ) : (
            <Link
              className="vehicle-pagination-button"
              href={resultsPageHref(routePath, identifier, mode, lossPage.page - 1)}
            >
              Previous
            </Link>
          )}
          <span className="vehicle-pagination-meta" aria-live="polite">
            Page {lossPage.page} of {lossPage.totalPages}
          </span>
          {lossPage.page >= lossPage.totalPages ? (
            <span
              className="vehicle-pagination-button vehicle-pagination-disabled"
              aria-disabled="true"
            >
              Next
            </span>
          ) : (
            <Link
              className="vehicle-pagination-button"
              href={resultsPageHref(routePath, identifier, mode, lossPage.page + 1)}
            >
              Next
            </Link>
          )}
        </nav>
      ) : null}
    </section>
  );
}

const LossMaintenancePageContent = renderLossMaintenancePageContent;

async function renderLossMaintenancePageContent({
  searchParams,
  routePath = "/losses/maintenance",
}: LossMaintenancePageProps) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired")
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath={routePath} />
      </main>
    );
  if (session.status === "unavailable")
    return (
      <main className="page-shell vehicle-page-shell">
        <ApiUnavailable routePath={routePath} />
      </main>
    );
  if (
    !session.roles.some(
      (role) => role.localeCompare("Losses", undefined, { sensitivity: "accent" }) === 0,
    )
  ) {
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <p className="eyebrow">Access restricted</p>
          <h2>You do not have permission to maintain Losses.</h2>
        </section>
      </main>
    );
  }

  const query = await searchParams;
  const identifier = (queryValue(query.identifier) ?? queryValue(query.txtGGNum) ?? "").trim();
  const mode =
    (queryValue(query.mode) ?? queryValue(query.radio) ?? "GG").toUpperCase() === "GP"
      ? "GP"
      : "GG";
  const page = Number(queryValue(query.page));
  const requestedPage = Number.isSafeInteger(page) && page > 0 ? page : 1;
  const submitted = Boolean(identifier);
  const errorMessage = queryValue(query.error);
  const savedMessage = queryValue(query.saved);
  let lossPage: LossPage | null = null;

  if (submitted) {
    try {
      lossPage = await getLossesPage({
        identifier,
        mode,
        page: requestedPage,
        pageSize: DEFAULT_LOSS_PAGE_SIZE,
      });
    } catch (error) {
      if (error instanceof LossApiError && error.reason === "unauthorized")
        return (
          <main className="page-shell vehicle-page-shell">
            <SessionRecovery returnPath={routePath} />
          </main>
        );
      console.error(
        "FIS loss maintenance request failed",
        error instanceof Error ? error.message : "unknown error",
      );
      return (
        <main className="page-shell vehicle-page-shell">
          <ApiUnavailable routePath={routePath} />
        </main>
      );
    }
  }

  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-labelledby="loss-maintenance-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Losses</p>
            <h1 id="loss-maintenance-title">Vehicle Losses Maintenance</h1>
            <p>Search by GG or GP number, then add or edit the vehicle loss history.</p>
          </div>
          <Link className="button button-secondary" href="/Losses/MNTLosses.aspx">
            Losses Menu
          </Link>
        </header>
        {errorMessage ? (
          <div className="notice notice-error" role="alert">
            {errorMessage}
          </div>
        ) : null}
        {savedMessage === "created" ? (
          <div className="notice notice-success" role="status">
            The loss record was saved.
          </div>
        ) : null}
        {savedMessage === "deleted" ? (
          <div className="notice notice-success" role="status">
            The loss record was deleted.
          </div>
        ) : null}
        <form className="vehicle-status-maintenance-panel" method="get">
          <div className="form-grid">
            <div className="form-field">
              <span className="form-label">Number Type</span>
              <div className="contract-search-radio">
                <label className="form-radio-label">
                  <input defaultChecked={mode === "GG"} name="mode" type="radio" value="GG" /> GG
                </label>
                <label className="form-radio-label">
                  <input defaultChecked={mode === "GP"} name="mode" type="radio" value="GP" /> GP
                  Number
                </label>
              </div>
            </div>
            <div className="form-field">
              <label className="form-label" htmlFor="loss-vehicle-identifier">
                {mode === "GP" ? "GP Number" : "GG Number"}
              </label>
              <input
                className="form-input"
                defaultValue={identifier}
                id="loss-vehicle-identifier"
                name="identifier"
                maxLength={30}
              />
            </div>
          </div>
          <div className="button-row">
            <button className="button button-primary" type="submit">
              Submit
            </button>
            <Link className="button button-secondary" href={routePath}>
              Menu
            </Link>
          </div>
        </form>
        {submitted && lossPage ? (
          <Results lossPage={lossPage} routePath={routePath} identifier={identifier} mode={mode} />
        ) : (
          <section className="vehicle-status-card">
            <p className="eyebrow">Search required</p>
            <h2>Enter a vehicle number to view loss history.</h2>
            <p className="muted-copy">
              The legacy workflow searches the vehicle master first, then loads losses sorted by
              loss date.
            </p>
          </section>
        )}
      </section>
    </main>
  );
}

export default function LossMaintenancePage(props: LossMaintenancePageProps) {
  return (
    <StreamedRoute>
      <LossMaintenancePageContent {...props} />
    </StreamedRoute>
  );
}

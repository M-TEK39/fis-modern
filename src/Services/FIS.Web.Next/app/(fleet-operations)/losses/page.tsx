import DataTableHeader from "@/components/ui/data-table-header";

import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import { StreamedRoute } from "@/components/app-shell/streamed-route";
import { MenuSection } from "@/components/ui/menu-section";
import {
  DEFAULT_LOSS_PAGE_SIZE,
  getLossesPage,
  LossApiError,
} from "@/lib/api/fleet-operations/api-losses";
import { getSession } from "@/lib/auth/session";

const LOSS_ROLE = "Losses";
type SearchParams = Promise<Record<string, string | string[] | undefined>>;

function hasLossRole(roles: readonly string[]) {
  return roles.some(
    (role) => role.localeCompare(LOSS_ROLE, undefined, { sensitivity: "accent" }) === 0,
  );
}

function valueOrDash(value: string | number | null | undefined) {
  return value === null || value === undefined || String(value).trim() === "" ? "-" : String(value);
}

function statusFor(loss: { lossStatus: string | null; cancelled: boolean }) {
  return loss.cancelled ? "Cancelled" : loss.lossStatus || "Open";
}

function queryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

function requestedPage(value: string | undefined) {
  const candidate = Number(value);
  return Number.isSafeInteger(candidate) && candidate > 0 ? candidate : 1;
}

function pageHref(page: number) {
  return page === 1 ? "/losses" : `/losses?page=${page}`;
}

function ApiUnavailable() {
  return (
    <section className="vehicle-status-card" role="alert">
      <p className="eyebrow">API unavailable</p>
      <h2>Losses could not be loaded.</h2>
      <p className="muted-copy">
        The application is still running. Retry when the FIS API is available.
      </p>
      <Link className="button button-primary" href="/losses">
        Try again
      </Link>
    </section>
  );
}

const LossesContent = renderLossesContent;

async function renderLossesContent({ searchParams }: Readonly<{ searchParams: SearchParams }>) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired") return <SessionRecovery returnPath="/losses" />;
  if (session.status === "unavailable") return <ApiUnavailable />;
  if (!hasLossRole(session.roles)) {
    return (
      <section className="vehicle-status-card" role="alert">
        <p className="eyebrow">Access restricted</p>
        <h2>You do not have permission to access Losses.</h2>
        <p className="muted-copy">This menu requires the Losses role.</p>
      </section>
    );
  }

  const query = await searchParams;
  const page = requestedPage(queryValue(query.page));

  let lossPage;
  try {
    lossPage = await getLossesPage({ page, pageSize: DEFAULT_LOSS_PAGE_SIZE });
  } catch (error) {
    if (error instanceof LossApiError && error.reason === "unauthorized")
      return <SessionRecovery returnPath="/losses" />;
    console.error(
      "FIS losses menu request failed",
      error instanceof Error ? error.message : "unknown error",
    );
    return <ApiUnavailable />;
  }

  return (
    <section className="vehicle-card" aria-labelledby="losses-title">
      <header className="vehicle-page-header">
        <div>
          <p className="eyebrow">Losses</p>
          <h1 id="losses-title">Losses Maintenance Menu</h1>
          <p>Review and maintain the vehicle loss records used by the legacy fleet process.</p>
        </div>
        <Link className="button button-secondary" href="/home">
          Home
        </Link>
      </header>

      <div className="vehicle-menu-tiles">
        <MenuSection title="Losses Maintenance Menu">
          <Link className="vehicle-menu-link" href="/Losses/Doc/Doc_losses.htm">
            Losses Maintenance Information / Help
          </Link>
        </MenuSection>
        <MenuSection title="Losses Maintenance">
          <Link className="vehicle-menu-link" href="/Losses/MNT_Loss_GetGg.aspx">
            1) Vehicle Losses Maintenance
          </Link>
        </MenuSection>
      </div>

      <section className="vehicle-status-maintenance-panel" aria-labelledby="loss-preview-title">
        <div className="vehicle-form-section-header">
          <div>
            <p className="eyebrow">{lossPage.total} active records</p>
            <h2 id="loss-preview-title">Vehicle Loss Preview</h2>
          </div>
        </div>
        {lossPage.items.length === 0 ? (
          <p className="muted-copy">No active loss records found.</p>
        ) : (
          <div className="vehicle-table-wrapper">
            <table className="vehicle-table">
              <caption className="sr-only">Vehicle loss records</caption>
              <DataTableHeader
                columns={[
                  { key: "column-1", label: <>Loss Date</> },
                  { key: "column-2", label: <>Reference</> },
                  { key: "column-3", label: <>Vehicle</> },
                  { key: "column-4", label: <>Type</> },
                  { key: "column-5", label: <>Status</> },
                  { key: "column-6", label: <>Amount</> },
                  { key: "column-7", label: <>Actions</> },
                ]}
              />
              <tbody>
                {lossPage.items.map((loss) => (
                  <tr key={loss.lossCode}>
                    <td>{valueOrDash(loss.lossDate?.slice(0, 10))}</td>
                    <td>{valueOrDash(loss.lossReference)}</td>
                    <td>{valueOrDash(loss.vehicleIdentifier ?? loss.vmfCode)}</td>
                    <td>{valueOrDash(loss.lossTypeDescription ?? loss.lossTypeCode)}</td>
                    <td>{statusFor(loss)}</td>
                    <td>{loss.lossAmount === null ? "-" : loss.lossAmount.toFixed(2)}</td>
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
        {lossPage.totalPages > 1 ? (
          <nav className="vehicle-pagination" aria-label="Loss preview pages">
            {lossPage.page > 1 ? (
              <Link className="vehicle-pagination-button" href={pageHref(lossPage.page - 1)}>
                Previous
              </Link>
            ) : (
              <span
                className="vehicle-pagination-button vehicle-pagination-disabled"
                aria-disabled="true"
              >
                Previous
              </span>
            )}
            <span className="vehicle-pagination-meta" aria-live="polite">
              Page {lossPage.page} of {lossPage.totalPages}
            </span>
            {lossPage.page < lossPage.totalPages ? (
              <Link className="vehicle-pagination-button" href={pageHref(lossPage.page + 1)}>
                Next
              </Link>
            ) : (
              <span
                className="vehicle-pagination-button vehicle-pagination-disabled"
                aria-disabled="true"
              >
                Next
              </span>
            )}
          </nav>
        ) : null}
        <div className="pagination-meta">
          Total records: {lossPage.total} | Page size: {lossPage.pageSize}
        </div>
      </section>
    </section>
  );
}

export default function LossesPage({ searchParams }: Readonly<{ searchParams: SearchParams }>) {
  return (
    <main className="page-shell vehicle-page-shell">
      <StreamedRoute>
        <LossesContent searchParams={searchParams} />
      </StreamedRoute>
    </main>
  );
}

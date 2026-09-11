import DataTableHeader from "@/components/ui/data-table-header";

import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import { StreamedRoute } from "@/components/app-shell/streamed-route";
import { MenuSection } from "@/components/ui/menu-section";
import {
  AccessRestricted,
  ActionNotice,
  ApiUnavailable,
  FmlFrame,
} from "@/app/(fleet-operations)/full-maintenance-lease/_components";
import {
  formatDate,
  getStatusClass,
  getStatusLabel,
  hasFmlPermission,
  termNotes,
  vehicleLabel,
  valueOrDash,
} from "@/app/(fleet-operations)/full-maintenance-lease/_utils";
import {
  DEFAULT_LEASE_TERMS_PAGE_SIZE,
  FmlApiError,
  getLeaseTermsPage,
  type LeaseTermsPage,
} from "@/lib/api/finance/api-fml";
import { getVehicleOptions, type VehicleOption } from "@/lib/api/vehicles/api-vehicles";
import { getSession } from "@/lib/auth/session";

type SearchParams = Promise<Record<string, string | string[] | undefined>>;

function firstQueryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

function positivePage(value: string | undefined) {
  const parsed = Number(value);
  return Number.isSafeInteger(parsed) && parsed > 0 ? parsed : 1;
}

function lookupPageHref(search: string, mode: string, page: number) {
  const params = new URLSearchParams();
  if (search.trim()) params.set("search", search);
  if (mode) params.set("mode", mode);
  if (page > 1) params.set("page", String(page));
  const query = params.toString();
  return query ? `/full-maintenance-lease?${query}` : "/full-maintenance-lease";
}

const FullMaintenanceLeasePageContent = renderFullMaintenanceLeasePageContent;

async function renderFullMaintenanceLeasePageContent({
  searchParams,
}: Readonly<{ searchParams: SearchParams }>) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired")
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath="/full-maintenance-lease" />
      </main>
    );
  if (session.status === "unavailable")
    return (
      <main className="page-shell vehicle-page-shell">
        <ApiUnavailable message="Full Maintenance Lease could not be opened." />
      </main>
    );
  if (!hasFmlPermission(session.accessLevel))
    return (
      <main className="page-shell vehicle-page-shell">
        <AccessRestricted />
      </main>
    );

  const query = await searchParams;
  const search = firstQueryValue(query.search) ?? "";
  const mode = firstQueryValue(query.mode)?.toUpperCase() === "GP" ? "GP" : "GG";
  const requestedPage = positivePage(firstQueryValue(query.page));
  const result = firstQueryValue(query.result);
  const message = firstQueryValue(query.message);
  let termsPage: LeaseTermsPage | null = null;
  let vehicles: VehicleOption[] | undefined;
  let lookupError = false;

  if (search.trim()) {
    try {
      [termsPage, vehicles] = await Promise.all([
        getLeaseTermsPage({
          page: requestedPage,
          pageSize: DEFAULT_LEASE_TERMS_PAGE_SIZE,
          search,
          mode,
        }),
        getVehicleOptions(),
      ]);
    } catch (error) {
      lookupError = error instanceof FmlApiError;
    }
  }

  const terms = termsPage?.items ?? [];
  const labels = new Map(
    (vehicles ?? []).map((vehicle) => [vehicle.vmfCode, vehicleLabel(vehicle)]),
  );
  return (
    <FmlFrame
      title="Full Maintenance Lease"
      description="Capture, approve, extend, and report on lease vehicle tariffs."
    >
      <ActionNotice result={result} message={message} />
      <div className="vehicle-menu-tiles">
        <MenuSection title="Full Maintenance Lease Information / Help">
          <Link className="vehicle-menu-link" href="/full-maintenance-lease/help">
            Open FML information and help
          </Link>
        </MenuSection>
        <MenuSection title="Lease Vehicle Section">
          <Link className="vehicle-menu-link" href="/full-maintenance-lease/tariffs">
            1) Capturing of Tariffs for Lease Vehicles
          </Link>
          <Link className="vehicle-menu-link" href="/full-maintenance-lease/upload">
            2) Import the Tariff File
          </Link>
          <Link className="vehicle-menu-link" href="/full-maintenance-lease/extend">
            3) Extend Latest Lease Tariff Period
          </Link>
          <Link className="vehicle-menu-link" href="/full-maintenance-lease/add-lease">
            4) Add Lease Tariff for In Service Vehicles
          </Link>
          <Link className="vehicle-menu-link" href="/full-maintenance-lease/reports">
            5) FML Reports
          </Link>
        </MenuSection>
      </div>

      <section className="vehicle-status-maintenance-panel" aria-labelledby="fml-lookup-title">
        <div className="vehicle-form-section-header">
          <div>
            <p className="eyebrow">Live records</p>
            <h2 id="fml-lookup-title">Quick lease lookup</h2>
          </div>
        </div>
        <form method="get" className="form-grid">
          <input type="hidden" name="page" value="1" />
          <div className="form-field">
            <label className="form-label" htmlFor="fml-search">
              Search number
            </label>
            <input
              className="form-input"
              id="fml-search"
              name="search"
              defaultValue={search}
              placeholder="GG fleet number or GP registration"
            />
          </div>
          <div className="form-field">
            <label className="form-label" htmlFor="fml-mode">
              Number type
            </label>
            <select className="form-select" id="fml-mode" name="mode" defaultValue={mode}>
              <option value="GG">GG</option>
              <option value="GP">GP</option>
            </select>
          </div>
          <div className="form-actions form-group-full">
            <button className="button button-primary" type="submit">
              Search
            </button>
            <Link className="button button-secondary" href="/full-maintenance-lease">
              Clear
            </Link>
          </div>
        </form>
        {lookupError ? <ApiUnavailable message="The lease lookup could not be completed." /> : null}
        {!lookupError && search.trim() && terms.length === 0 ? (
          <p className="muted-copy">No lease records matched that vehicle search.</p>
        ) : null}
        {terms.length > 0 ? (
          <div className="vehicle-table-wrapper">
            <table className="vehicle-table">
              <caption className="sr-only">Lease records matching the search</caption>
              <DataTableHeader
                columns={[
                  { key: "column-1", label: <>Lease #</> },
                  { key: "column-2", label: <>Vehicle</> },
                  { key: "column-3", label: <>Status</> },
                  { key: "column-4", label: <>Start</> },
                  { key: "column-5", label: <>End</> },
                  { key: "column-6", label: <>Notes</> },
                ]}
              />
              <tbody>
                {terms.map((term) => (
                  <tr key={term.termId}>
                    <td>{term.termId}</td>
                    <td>{labels.get(term.vmfCode) ?? term.vmfCode}</td>
                    <td>
                      <span className={getStatusClass(term.authorityStatus)}>
                        {getStatusLabel(term.authorityStatus)}
                      </span>
                    </td>
                    <td>{formatDate(term.startDate)}</td>
                    <td>{formatDate(term.endDate)}</td>
                    <td>{valueOrDash(termNotes(term))}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ) : null}
        {termsPage && termsPage.totalPages > 1 ? (
          <>
            <nav className="vehicle-pagination" aria-label="Lease lookup pages">
              {termsPage.page <= 1 ? (
                <span
                  className="vehicle-pagination-button vehicle-pagination-disabled"
                  aria-disabled="true"
                >
                  Previous
                </span>
              ) : (
                <Link
                  className="vehicle-pagination-button"
                  href={lookupPageHref(search, mode, termsPage.page - 1)}
                >
                  Previous
                </Link>
              )}
              <span className="vehicle-pagination-meta" aria-live="polite">
                Page {termsPage.page} of {termsPage.totalPages}
              </span>
              {termsPage.page >= termsPage.totalPages ? (
                <span
                  className="vehicle-pagination-button vehicle-pagination-disabled"
                  aria-disabled="true"
                >
                  Next
                </span>
              ) : (
                <Link
                  className="vehicle-pagination-button"
                  href={lookupPageHref(search, mode, termsPage.page + 1)}
                >
                  Next
                </Link>
              )}
            </nav>
            <div className="pagination-meta">
              Total records: {termsPage.total} | Page size: {termsPage.pageSize}
            </div>
          </>
        ) : null}
      </section>
    </FmlFrame>
  );
}

export default function FullMaintenanceLeasePage(props: Readonly<{ searchParams: SearchParams }>) {
  return (
    <StreamedRoute>
      <FullMaintenanceLeasePageContent {...props} />
    </StreamedRoute>
  );
}

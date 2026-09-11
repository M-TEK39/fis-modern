import DataTableHeader from "@/components/ui/data-table-header";

import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";
import { Suspense } from "react";

import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import { hasAssetVerificationAccess } from "@/app/(fleet-operations)/vehicle-verification/access";
import RouteLoading from "@/components/app-shell/route-loading";
import { MenuSection } from "@/components/ui/menu-section";
import {
  AssetVerificationApiError,
  DEFAULT_ASSET_VERIFICATION_PAGE_SIZE,
  getAssetVerificationsPage,
} from "@/lib/api/fleet-operations/api-asset-verification";
import { getSession } from "@/lib/auth/session";

type SearchParams = Promise<Record<string, string | string[] | undefined>>;
type Query = Record<string, string | string[] | undefined>;

function dateValue(value: string | null) {
  return value ? value.slice(0, 10) : "-";
}

function valueOrDash(value: string | number | null | undefined) {
  return value === null || value === undefined || String(value).trim() === "" ? "-" : String(value);
}

function queryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

function requestedPage(value: string | undefined) {
  const candidate = Number(value);
  return Number.isSafeInteger(candidate) && candidate > 0 ? candidate : 1;
}

function pageHref(query: Query, page: number) {
  const params = new URLSearchParams();
  for (const [key, value] of Object.entries(query)) {
    if (key === "page" || value === undefined) continue;
    for (const item of Array.isArray(value) ? value : [value]) params.append(key, item);
  }
  if (page > 1) params.set("page", String(page));
  const queryString = params.toString();
  return queryString ? `/vehicle-verification?${queryString}` : "/vehicle-verification";
}

const VehicleVerificationPageContent = renderVehicleVerificationPageContent;

async function renderVehicleVerificationPageContent({
  searchParams,
}: Readonly<{ searchParams: SearchParams }>) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired")
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath="/vehicle-verification" />
      </main>
    );
  if (session.status === "unavailable")
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <p className="eyebrow">API unavailable</p>
          <h1>Asset verification is unavailable.</h1>
          <p className="muted-copy">Retry when the FIS API is available.</p>
          <Link className="button button-primary" href="/vehicle-verification">
            Try again
          </Link>
        </section>
      </main>
    );
  if (!hasAssetVerificationAccess(session.roles, session.accessLevel))
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <p className="eyebrow">Access restricted</p>
          <h1>You do not have permission to access Vehicle Asset Verification.</h1>
        </section>
      </main>
    );

  const query = await searchParams;
  const page = requestedPage(queryValue(query.page));

  try {
    const verificationPage = await getAssetVerificationsPage({
      page,
      pageSize: DEFAULT_ASSET_VERIFICATION_PAGE_SIZE,
    });
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-card" aria-labelledby="asset-verification-title">
          <header className="vehicle-page-header">
            <div>
              <p className="eyebrow">Vehicle management</p>
              <h1 id="asset-verification-title">Vehicle Asset Verification</h1>
              <p>
                Maintain the client-era asset verification record while reading compatible modern
                fields when available.
              </p>
            </div>
            <Link className="button button-secondary" href="/home">
              Home
            </Link>
          </header>
          <div className="vehicle-menu-tiles">
            <MenuSection title="Asset Verification Maintenance Information / Help">
              <p className="muted-copy">
                Read the original asset verification maintenance guidance.
              </p>
              <Link
                className="button button-secondary button-small"
                href="/vehicle-verification/help"
              >
                Open help
              </Link>
            </MenuSection>
            <MenuSection title="Asset Verification Maintenance">
              <div className="button-row">
                <Link
                  className="button button-primary button-small"
                  href="/vehicle-verification/add"
                >
                  1) Add Asset Verification Details
                </Link>
                <Link
                  className="button button-secondary button-small"
                  href="/vehicle-verification/edit"
                >
                  2) Edit Asset Verification Details
                </Link>
              </div>
            </MenuSection>
            <MenuSection title="Asset Verification Reports">
              <p className="muted-copy">Open the existing Asset Verification report menu.</p>
              <Link
                className="button button-secondary button-small"
                href="/reports/asset-verification"
              >
                Open reports
              </Link>
            </MenuSection>
          </div>
          <section
            className="vehicle-status-maintenance-panel"
            aria-labelledby="asset-verification-preview-title"
          >
            <div className="vehicle-form-section-header">
              <div>
                <p className="eyebrow">Current records</p>
                <h2 id="asset-verification-preview-title">Asset Verification Preview</h2>
              </div>
              <span className="form-hint">{verificationPage.total} record(s)</span>
            </div>
            {verificationPage.items.length === 0 ? (
              <p className="muted-copy">No asset verification records were found.</p>
            ) : (
              <div className="vehicle-table-wrapper">
                <table className="vehicle-table">
                  <caption className="sr-only">Asset verification records</caption>
                  <DataTableHeader
                    columns={[
                      { key: "column-1", label: <>Registration</> },
                      { key: "column-2", label: <>GG Code</> },
                      { key: "column-3", label: <>Department</> },
                      { key: "column-4", label: <>Site</> },
                      { key: "column-5", label: <>Verified</> },
                      { key: "column-6", label: <>Status</> },
                      { key: "column-7", label: <>Manager</> },
                      { key: "column-8", label: <>Action</> },
                    ]}
                  />
                  <tbody>
                    {verificationPage.items.map((record) => (
                      <tr key={record.assetVerificationCode}>
                        <td>{valueOrDash(record.vehicleRegNo)}</td>
                        <td>{valueOrDash(record.vmfCode)}</td>
                        <td>{valueOrDash(record.departmentName)}</td>
                        <td>{valueOrDash(record.siteName ?? record.siteCode)}</td>
                        <td>{dateValue(record.dateLastVerified ?? record.verificationDate)}</td>
                        <td>{valueOrDash(record.verificationStatus)}</td>
                        <td>{valueOrDash(record.responsibleManager)}</td>
                        <td>
                          <Link
                            className="button button-secondary button-small"
                            href={`/vehicle-verification/edit/details?gg=${encodeURIComponent(record.vehicleRegNo ?? String(record.vmfCode ?? ""))}`}
                          >
                            Edit
                          </Link>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
            {verificationPage.totalPages > 1 ? (
              <nav className="vehicle-pagination" aria-label="Asset verification preview pages">
                {verificationPage.page > 1 ? (
                  <Link
                    className="vehicle-pagination-button"
                    href={pageHref(query, verificationPage.page - 1)}
                  >
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
                  Page {verificationPage.page} of {verificationPage.totalPages}
                </span>
                {verificationPage.page < verificationPage.totalPages ? (
                  <Link
                    className="vehicle-pagination-button"
                    href={pageHref(query, verificationPage.page + 1)}
                  >
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
              Total records: {verificationPage.total} | Page size: {verificationPage.pageSize}
            </div>
          </section>
        </section>
      </main>
    );
  } catch (error) {
    if (error instanceof AssetVerificationApiError && error.reason === "unauthorized")
      return (
        <main className="page-shell vehicle-page-shell">
          <SessionRecovery returnPath="/vehicle-verification" />
        </main>
      );
    console.error(
      "FIS asset verification list request failed",
      error instanceof Error ? error.message : "unknown error",
    );
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <p className="eyebrow">API unavailable</p>
          <h1>Asset verification records could not be loaded.</h1>
          <Link className="button button-primary" href="/vehicle-verification">
            Try again
          </Link>
        </section>
      </main>
    );
  }
}

export default function VehicleVerificationPage({
  searchParams,
}: Readonly<{ searchParams: SearchParams }>) {
  return (
    <Suspense fallback={<RouteLoading />}>
      <VehicleVerificationPageContent searchParams={searchParams} />
    </Suspense>
  );
}

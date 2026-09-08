import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import { deleteAuctionAction } from "@/app/auction/actions";
import SessionRecovery from "@/app/home/session-recovery";
import { AuctionApiError, getAuction, type AuctionRecord } from "@/lib/api-auction";
import { getSession } from "@/lib/session";

const REPORTS_ROLE = "Reports";

export type AuctionDeleteDetailPageProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
  routePath?: string;
};

function getQueryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

function getPositiveQueryInt(value: string | undefined) {
  const parsed = Number(value);
  return value && Number.isInteger(parsed) && parsed > 0 ? parsed : null;
}

function hasReportsRole(roles: readonly string[]) {
  return roles.some(
    (role) => role.localeCompare(REPORTS_ROLE, undefined, { sensitivity: "accent" }) === 0,
  );
}

function valueOrDash(value: string | number | null | undefined) {
  return value === null || value === undefined || String(value).trim() === "" ? "-" : String(value);
}

function formatDate(value: string | null) {
  return value?.slice(0, 10) || "-";
}

function DetailField({
  label,
  value,
}: Readonly<{ label: string; value: string | number | null | undefined }>) {
  return (
    <div className="form-field">
      <span className="form-label">{label}</span>
      <div className="form-readonly-value">{valueOrDash(value)}</div>
    </div>
  );
}

function AuctionDetails({
  auction,
  routePath,
}: Readonly<{ auction: AuctionRecord; routePath: string }>) {
  return (
    <section
      className="vehicle-status-maintenance-panel"
      aria-labelledby="auction-delete-detail-title"
    >
      <div className="vehicle-form-section-header">
        <div>
          <p className="eyebrow">Delete confirmation</p>
          <h2 id="auction-delete-detail-title">Auction #{auction.auctionCode}</h2>
          <p>
            {valueOrDash(auction.fleetNumber)} / {valueOrDash(auction.registrationNumber)} (
            {auction.vmfCode})
          </p>
        </div>
      </div>
      <div className="form-grid">
        <DetailField label="Auction Number" value={auction.auctionNumber} />
        <DetailField label="Camp" value={auction.camp} />
        <DetailField label="Lot Number" value={auction.lot} />
        <DetailField label="Auction Garage" value={auction.auctionGarage} />
        <DetailField label="Bar Code" value={auction.barcode} />
        <DetailField label="Auth Number" value={auction.authNumber} />
        <DetailField label="Auth Date" value={formatDate(auction.authDate)} />
        <DetailField label="Km" value={auction.auctionKm} />
        <DetailField label="Garage Owner" value={auction.garageOwner} />
        <DetailField label="Sold Reason" value={auction.reasonSold} />
        <DetailField label="Estimate Price" value={auction.estimateAmount} />
        <DetailField label="Reserve Price" value={auction.reserveAmount} />
        <DetailField label="Sold Price" value={auction.soldAmount} />
        <DetailField label="Sold To Name" value={auction.soldTo} />
        <DetailField label="Sold To ID" value={auction.soldId} />
        <DetailField label="Sold Date" value={formatDate(auction.soldDate)} />
        <DetailField label="Remarks" value={auction.remark} />
      </div>
      <form action={deleteAuctionAction}>
        <input name="auctionCode" type="hidden" value={auction.auctionCode} />
        <input name="returnPath" type="hidden" value={routePath} />
        <div className="button-row">
          <button className="button button-danger" type="submit">
            DELETE
          </button>
          <Link className="button button-secondary" href="/auction/delete-vehicle">
            Cancel
          </Link>
        </div>
      </form>
    </section>
  );
}

function ApiUnavailable() {
  return (
    <section className="vehicle-status-card" role="alert">
      <div className="status-icon status-icon-error" aria-hidden="true">
        !
      </div>
      <p className="eyebrow">API unavailable</p>
      <h2>Auction details could not be loaded.</h2>
      <p className="muted-copy">
        The application is still running. Retry when the FIS API is available.
      </p>
      <Link className="button button-primary" href="/auction/delete-vehicle">
        Back to Auction
      </Link>
    </section>
  );
}

export default async function AuctionDeleteDetailPage({
  searchParams,
  routePath = "/auction/delete-vehicle/detail",
}: AuctionDeleteDetailPageProps) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") {
    redirect("/login");
  }
  if (session.status === "expired") {
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath={routePath} />
      </main>
    );
  }
  if (session.status === "unavailable") {
    return (
      <main className="page-shell vehicle-page-shell">
        <ApiUnavailable />
      </main>
    );
  }
  if (!hasReportsRole(session.roles)) {
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <p className="eyebrow">Access restricted</p>
          <h2>You do not have permission to delete Auction records.</h2>
        </section>
      </main>
    );
  }

  const query = await searchParams;
  const auctionCode = getPositiveQueryInt(
    getQueryValue(query.auctionId) ?? getQueryValue(query.ACode),
  );
  if (!auctionCode) {
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <p className="eyebrow">Auction not selected</p>
          <h2>Select an auction record from the deletion list.</h2>
          <Link className="button button-secondary" href="/auction/delete-vehicle">
            Back to Auction
          </Link>
        </section>
      </main>
    );
  }

  try {
    const auction = await getAuction(auctionCode);
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-card" aria-labelledby="auction-delete-page-title">
          <header className="vehicle-page-header">
            <div>
              <p className="eyebrow">Auction maintenance</p>
              <h1 id="auction-delete-page-title">Delete Auction</h1>
              <p>Review the legacy auction and vehicle fields before confirming deletion.</p>
            </div>
            <Link className="button button-secondary" href="/auction">
              Auction Menu
            </Link>
          </header>
          <AuctionDetails auction={auction} routePath={routePath} />
        </section>
      </main>
    );
  } catch (requestError) {
    if (requestError instanceof AuctionApiError && requestError.reason === "unauthorized") {
      return (
        <main className="page-shell vehicle-page-shell">
          <SessionRecovery returnPath={`${routePath}?auctionId=${auctionCode}`} />
        </main>
      );
    }
    if (requestError instanceof AuctionApiError && requestError.reason === "not-found") {
      return (
        <main className="page-shell vehicle-page-shell">
          <section className="vehicle-status-card" role="alert">
            <p className="eyebrow">Record not found</p>
            <h2>Auction #{auctionCode} was not found.</h2>
            <Link className="button button-secondary" href="/auction/delete-vehicle">
              Back to Auction
            </Link>
          </section>
        </main>
      );
    }
    console.error(
      "FIS auction deletion detail request failed",
      requestError instanceof Error ? requestError.message : "unknown error",
    );
    return (
      <main className="page-shell vehicle-page-shell">
        <ApiUnavailable />
      </main>
    );
  }
}

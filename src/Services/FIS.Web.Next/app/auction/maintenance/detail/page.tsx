import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import { saveAuctionMaintenanceAction } from "@/app/auction/actions";
import SessionRecovery from "@/app/home/session-recovery";
import { AuctionApiError, getAuction, type AuctionRecord } from "@/lib/api-auction";
import { getSession } from "@/lib/session";

const REPORTS_ROLE = "Reports";

export type AuctionDetailPageProps = {
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
  return roles.some((role) => role.localeCompare(REPORTS_ROLE, undefined, { sensitivity: "accent" }) === 0);
}

function valueOrDash(value: string | number | null | undefined) {
  return value === null || value === undefined || String(value).trim() === "" ? "-" : String(value);
}

function dateInputValue(value: string | null | undefined) {
  return value?.slice(0, 10) ?? "";
}

function AuctionForm({ auction, routePath, updated, error }: Readonly<{ auction: AuctionRecord; routePath: string; updated: boolean; error?: string }>) {
  const reasonOptions = [
    "Old & Obsolete",
    "Obsolete",
    "Old & Uneconomical",
    "Uneconomical",
    "Stolen & Recovered",
    "Accident",
  ];

  return (
    <form className="vehicle-status-maintenance-panel" action={saveAuctionMaintenanceAction}>
      <input name="auctionCode" type="hidden" value={auction.auctionCode} />
      <input name="vmfCode" type="hidden" value={auction.vmfCode} />
      <input name="returnPath" type="hidden" value={`${routePath}?auctionId=${auction.auctionCode}`} />
      {updated ? <div className="notice notice-success" role="status">Auction record updated successfully.</div> : null}
      {error ? <div className="notice notice-error" role="alert">{error}</div> : null}
      <div className="vehicle-form-section-header">
        <div><p className="eyebrow">Existing record</p><h2>Edit Auction #{auction.auctionCode}</h2><p>{valueOrDash(auction.fleetNumber)} / {valueOrDash(auction.registrationNumber)} ({auction.vmfCode})</p></div>
      </div>
      <div className="form-grid">
        <div className="form-field">
          <label className="form-label" htmlFor="auction-number">Auction Number</label>
          <input className="form-input" id="auction-number" maxLength={7} name="auctionNumber" defaultValue={auction.auctionNumber ?? ""} required />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="auction-camp">Camp</label>
          <select className="form-select" id="auction-camp" name="camp" defaultValue={auction.camp ?? "Camp1"} required>
            <option value="Camp1">Camp1</option>
            <option value="Camp2">Camp2</option>
          </select>
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="auction-lot">Lot Number</label>
          <input className="form-input" id="auction-lot" min="0" name="lot" type="number" defaultValue={auction.lot ?? ""} required />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="auction-garage">Auction Garage</label>
          <input className="form-input" id="auction-garage" readOnly value={auction.auctionGarage ?? 1} />
          <input name="auctionGarage" type="hidden" value={auction.auctionGarage ?? 1} />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="auction-barcode">Bar Code</label>
          <input className="form-input" id="auction-barcode" maxLength={15} name="barcode" defaultValue={auction.barcode ?? ""} />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="auction-auth-number">Auth Number</label>
          <input className="form-input" id="auction-auth-number" maxLength={10} name="authNumber" defaultValue={auction.authNumber ?? ""} />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="auction-auth-date">Auth Date</label>
          <input className="form-input" id="auction-auth-date" name="authDate" type="date" defaultValue={dateInputValue(auction.authDate)} />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="auction-km">Km</label>
          <input className="form-input" id="auction-km" min="0" name="auctionKm" type="number" defaultValue={auction.auctionKm ?? ""} required />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="auction-owner">Garage Owner</label>
          <select className="form-select" id="auction-owner" name="garageOwner" defaultValue={auction.garageOwner ?? "Jhb"} required>
            <option value="Jhb">Jhb</option>
            <option value="Pta">Pta</option>
          </select>
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="auction-reason">Sold Reason</label>
          <select className="form-select" id="auction-reason" name="reasonSold" defaultValue={auction.reasonSold ?? "Old & Obsolete"} required>
            {reasonOptions.map((reason) => <option key={reason} value={reason}>{reason}</option>)}
          </select>
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="auction-estimate">Estimate Price</label>
          <input className="form-input" id="auction-estimate" min="0" name="estimateAmount" type="number" defaultValue={auction.estimateAmount ?? ""} required />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="auction-reserve">Reserve Price</label>
          <input className="form-input" id="auction-reserve" min="0" name="reserveAmount" type="number" defaultValue={auction.reserveAmount ?? ""} required />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="auction-sold-price">Sold Price</label>
          <input className="form-input" id="auction-sold-price" min="0" name="soldAmount" type="number" defaultValue={auction.soldAmount ?? ""} required />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="auction-sold-to">Sold To Name</label>
          <input className="form-input" id="auction-sold-to" maxLength={30} name="soldTo" defaultValue={auction.soldTo ?? ""} />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="auction-sold-id">Sold To ID</label>
          <input className="form-input" id="auction-sold-id" maxLength={13} name="soldId" defaultValue={auction.soldId ?? ""} />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="auction-sold-date">Sold Date</label>
          <input className="form-input" id="auction-sold-date" name="soldDate" type="date" defaultValue={dateInputValue(auction.soldDate)} />
        </div>
        <div className="form-field form-group-full">
          <label className="form-label" htmlFor="auction-remark">Remarks</label>
          <input className="form-input" id="auction-remark" maxLength={30} name="remark" defaultValue={auction.remark ?? ""} />
        </div>
      </div>
      <div className="button-row">
        <button className="button button-primary" type="submit">Update</button>
        <Link className="button button-secondary" href="/auction/maintenance">Menu</Link>
      </div>
    </form>
  );
}

function ApiUnavailable() {
  return (
    <section className="vehicle-status-card" role="alert">
      <div className="status-icon status-icon-error" aria-hidden="true">!</div>
      <p className="eyebrow">API unavailable</p>
      <h2>Auction details could not be loaded.</h2>
      <p className="muted-copy">The application is still running. Retry when the FIS API is available.</p>
      <Link className="button button-primary" href="/auction/maintenance">Back to Auction</Link>
    </section>
  );
}

export default async function AuctionDetailPage({ searchParams, routePath = "/auction/maintenance/detail" }: AuctionDetailPageProps) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") {
    redirect("/login");
  }
  if (session.status === "expired") {
    return <main className="page-shell vehicle-page-shell"><SessionRecovery returnPath={routePath} /></main>;
  }
  if (session.status === "unavailable") {
    return <main className="page-shell vehicle-page-shell"><ApiUnavailable /></main>;
  }
  if (!hasReportsRole(session.roles)) {
    return <main className="page-shell vehicle-page-shell"><section className="vehicle-status-card" role="alert"><p className="eyebrow">Access restricted</p><h2>You do not have permission to maintain Auction records.</h2></section></main>;
  }

  const query = await searchParams;
  const auctionCode = getPositiveQueryInt(getQueryValue(query.auctionId) ?? getQueryValue(query.ACode));
  const updated = getQueryValue(query.updated) === "1";
  const error = getQueryValue(query.error);
  if (!auctionCode) {
    return <main className="page-shell vehicle-page-shell"><section className="vehicle-status-card" role="alert"><p className="eyebrow">Auction not selected</p><h2>Select an auction record from the maintenance list.</h2><Link className="button button-secondary" href="/auction/maintenance">Back to Auction</Link></section></main>;
  }

  try {
    const auction = await getAuction(auctionCode);
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-card" aria-labelledby="auction-detail-title">
          <header className="vehicle-page-header">
            <div><p className="eyebrow">Auction maintenance</p><h1 id="auction-detail-title">Auction Maintenance Detail</h1><p>Update the complete legacy auction and vehicle sale record.</p></div>
            <Link className="button button-secondary" href="/auction">Auction Menu</Link>
          </header>
          <AuctionForm auction={auction} routePath={routePath} updated={updated} error={error} />
        </section>
      </main>
    );
  } catch (requestError) {
    if (requestError instanceof AuctionApiError && requestError.reason === "unauthorized") {
      return <main className="page-shell vehicle-page-shell"><SessionRecovery returnPath={`${routePath}?auctionId=${auctionCode}`} /></main>;
    }
    if (requestError instanceof AuctionApiError && requestError.reason === "not-found") {
      return <main className="page-shell vehicle-page-shell"><section className="vehicle-status-card" role="alert"><p className="eyebrow">Record not found</p><h2>Auction #{auctionCode} was not found.</h2><Link className="button button-secondary" href="/auction/maintenance">Back to Auction</Link></section></main>;
    }
    console.error("FIS auction detail request failed", requestError instanceof Error ? requestError.message : "unknown error");
    return <main className="page-shell vehicle-page-shell"><ApiUnavailable /></main>;
  }
}

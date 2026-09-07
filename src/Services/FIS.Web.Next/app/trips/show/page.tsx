import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";
import type { ReactNode } from "react";

import { closeTripAuthorityAction } from "@/app/trips/actions";
import { hasTripAuthorityAccess, getTripSession, queryValue, tripAccessRestricted, tripSessionMessage } from "@/app/trips/_page";
import PrintButton from "@/app/contracts/print-button";
import SessionRecovery from "@/app/home/session-recovery";
import { ContractApiError, getContract, type ContractRecord } from "@/lib/api-contracts";
import { getDepartment, type DepartmentRecord } from "@/lib/api-departments";
import { getSite, type SiteRecord } from "@/lib/api-sites";
import { getTripAuthorityDetails, TripAuthorityApiError, type TripAuthorityDetails, type TripAuthorityRoute } from "@/lib/api-trip-authorities";
import { getVehicleForStatus, type VehicleStatusVehicle } from "@/lib/api-vehicle-status";

type SearchParams = Promise<Record<string, string | string[] | undefined>>;
type Tab = "vehicle" | "trip" | "drivers" | "passengers" | "routes";

function positiveInteger(value: string) {
  const parsed = Number(value.trim());
  return Number.isSafeInteger(parsed) && parsed > 0 ? parsed : null;
}

function selectedTab(value: string): Tab {
  return value === "trip" || value === "drivers" || value === "passengers" || value === "routes" ? value : "vehicle";
}

function valueOrDash(value: string | number | null | undefined) {
  return value === null || value === undefined || String(value).trim() === "" ? "-" : String(value);
}

function formatDate(value: string | null) {
  return value?.slice(0, 10) || "-";
}

function formatBoolean(value: boolean) {
  return value ? "Yes" : "No";
}

function tripTypeLabel(value: number | null) {
  switch (value) {
    case 1:
      return "Normal (Official Business)";
    case 2:
      return "Emergency";
    case 3:
      return "Standby";
    default:
      return valueOrDash(value);
  }
}

function tabHref(tripId: number, tab: Tab) {
  return `/trips/show?tripId=${encodeURIComponent(tripId)}&tab=${tab}`;
}

function FactsTable({ caption, rows }: Readonly<{ caption: string; rows: readonly [string, ReactNode][] }>) {
  return <div className="vehicle-table-wrapper"><table className="vehicle-table"><caption className="sr-only">{caption}</caption><tbody>{rows.map(([label, value]) => <tr key={label}><th scope="row">{label}</th><td>{value}</td></tr>)}</tbody></table></div>;
}

function TripTabs({ tripId, activeTab }: Readonly<{ tripId: number; activeTab: Tab }>) {
  const tabs: readonly [Tab, string][] = [
    ["vehicle", "Vehicle Information"],
    ["trip", "Trip Information"],
    ["drivers", "Driver Information"],
    ["passengers", "Passenger Information"],
    ["routes", "Route Information"],
  ];

  return <nav className="reference-data-tabs" aria-label="Trip authority information"><>{tabs.map(([tab, label]) => <Link className={`reference-data-tab${activeTab === tab ? " active" : ""}`} aria-current={activeTab === tab ? "page" : undefined} href={tabHref(tripId, tab)} key={tab}>{label}</Link>)}</></nav>;
}

function VehicleInformation({ details, contract, vehicle, site, department }: Readonly<{ details: TripAuthorityDetails; contract: ContractRecord | null; vehicle: VehicleStatusVehicle | null; site: SiteRecord | null; department: DepartmentRecord | null }>) {
  const trip = details.trip;
  return <section className="vehicle-form-section" aria-labelledby="trip-vehicle-information-title"><div className="vehicle-form-section-header"><div><p className="eyebrow">Vehicle context</p><h2 id="trip-vehicle-information-title">Vehicle Information</h2></div></div><FactsTable caption="Trip authority vehicle information" rows={[
    ["Department Name", department?.description ?? valueOrDash(site?.departmentCode)],
    ["Site", site ? `${valueOrDash(site.description)} (${site.siteCode})` : valueOrDash(contract?.siteCode ?? trip.siteCode)],
    ["Contract ID", trip.contractCode],
    ["VMF Code", vehicle?.vmfCode ?? contract?.vmfCode ?? trip.vmfCode],
    ["Fleet Number", vehicle?.fleetNumber ?? contract?.fleetNumber],
    ["Make", vehicle?.typeName],
    ["Model", vehicle?.modelName],
    ["Registration Number", vehicle?.registrationNumber ?? contract?.registrationNumber],
    ["Start ODO Meter", contract?.startOdometer],
  ]} /></section>;
}

function TripInformation({ details, contract }: Readonly<{ details: TripAuthorityDetails; contract: ContractRecord | null }>) {
  const trip = details.trip;
  return <section className="vehicle-form-section" aria-labelledby="trip-information-title"><div className="vehicle-form-section-header"><div><p className="eyebrow">Authority record</p><h2 id="trip-information-title">Trip Information</h2></div></div><FactsTable caption="Trip authority information" rows={[
    ["Approver Name", trip.approverName],
    ["Approver Rank", trip.approverRank],
    ["Approver Telephone", trip.approverTelephone],
    ["Issue Date", formatDate(trip.issueDate)],
    ["Expiry Date", formatDate(trip.expiryDate)],
    ["End ODO Meter", trip.endOdometer],
    ["Trip Reason", trip.tripReason],
    ["Trip Type", tripTypeLabel(trip.tripTypeCode)],
    ["Trip Incident Type Code", trip.tripIncidentTypeCode],
    ["Trip Request Number", trip.tripRequestNumber],
    ["Trip Captured by", trip.userAccessCode ? `User ${trip.userAccessCode}` : null],
    ["Contract Driver ID", contract?.driverId],
    ["Locked for Transfer", formatBoolean(trip.lockedForTransfer)],
    ["Monthly Trip", formatBoolean(trip.tripIsMonthly)],
  ]} /></section>;
}

function DriverInformation({ details }: Readonly<{ details: TripAuthorityDetails }>) {
  return <section className="vehicle-form-section" aria-labelledby="trip-driver-information-title"><div className="vehicle-form-section-header"><div><p className="eyebrow">{details.drivers.length} record{details.drivers.length === 1 ? "" : "s"}</p><h2 id="trip-driver-information-title">Driver Information</h2></div></div>{details.drivers.length === 0 ? <p className="muted-copy">No driver records found for this trip.</p> : <div className="vehicle-table-wrapper"><table className="vehicle-table"><caption className="sr-only">Drivers assigned to trip authority {details.trip.tripId}</caption><thead><tr><th scope="col">Name</th><th scope="col">Driver ID</th><th scope="col">Passport</th><th scope="col">Licence</th><th scope="col">Primary</th><th scope="col">Active</th></tr></thead><tbody>{details.drivers.map((driver) => <tr key={driver.tripDriverCode}><td>{valueOrDash(driver.name)}</td><td>{valueOrDash(driver.identityNumber)}</td><td>{valueOrDash(driver.passportNumber)}</td><td>{valueOrDash(driver.licenceNumber)}</td><td>{formatBoolean(driver.isPrimary)}</td><td>{formatBoolean(driver.isActive)}</td></tr>)}</tbody></table></div>} </section>;
}

function PassengerInformation({ details }: Readonly<{ details: TripAuthorityDetails }>) {
  return <section className="vehicle-form-section" aria-labelledby="trip-passenger-information-title"><div className="vehicle-form-section-header"><div><p className="eyebrow">{details.passengers.length} record{details.passengers.length === 1 ? "" : "s"}</p><h2 id="trip-passenger-information-title">Passenger Information</h2></div></div>{details.passengers.length === 0 ? <p className="muted-copy">No passenger records found for this trip.</p> : <div className="vehicle-table-wrapper"><table className="vehicle-table"><caption className="sr-only">Passengers assigned to trip authority {details.trip.tripId}</caption><thead><tr><th scope="col">Passenger</th><th scope="col">Record</th></tr></thead><tbody>{details.passengers.map((passenger) => <tr key={passenger.tripPassengerCode}><td>{valueOrDash(passenger.name)}</td><td>{passenger.tripPassengerCode}</td></tr>)}</tbody></table></div>}</section>;
}

function routeStartOdometer(route: TripAuthorityRoute, previousEndOdometer: number | null) {
  return route.startOdometer ?? previousEndOdometer;
}

function RouteInformation({ details }: Readonly<{ details: TripAuthorityDetails }>) {
  let previousEndOdometer: number | null = null;
  return <section className="vehicle-form-section" aria-labelledby="trip-route-information-title"><div className="vehicle-form-section-header"><div><p className="eyebrow">{details.routes.length} route{details.routes.length === 1 ? "" : "s"}</p><h2 id="trip-route-information-title">Route Information</h2></div></div>{details.routes.length === 0 ? <p className="muted-copy">No route records found for this trip.</p> : <div className="vehicle-table-wrapper"><table className="vehicle-table"><caption className="sr-only">Routes for trip authority {details.trip.tripId}</caption><thead><tr><th scope="col">Route</th><th scope="col">Start date</th><th scope="col">End date</th><th scope="col">Start ODO</th><th scope="col">End ODO</th><th scope="col">Distance</th><th scope="col">Locations</th><th scope="col">BAS codes</th></tr></thead><tbody>{details.routes.map((route) => { const startOdometer = routeStartOdometer(route, previousEndOdometer); previousEndOdometer = route.endOdometer ?? startOdometer; return <tr key={route.routeCode}><td>{route.routeCode}</td><td>{formatDate(route.startDate)}</td><td>{formatDate(route.endDate)}</td><td>{valueOrDash(startOdometer)}</td><td>{valueOrDash(route.endOdometer)}</td><td>{valueOrDash(route.distance ?? route.estimatedDistance)}</td><td>{valueOrDash(route.startLocation)} → {valueOrDash(route.endLocation)}</td><td>Resp: {valueOrDash(route.responsibilityCode)}<br />Obj: {valueOrDash(route.objectiveCode)}<br />Project: {valueOrDash(route.projectNumber)}<br />Fund: {valueOrDash(route.fundCode)}</td></tr>; })}</tbody></table></div>}<p className="muted-copy">Route end odometers are entered in the close form below. The API validates the persisted route sequence and required BAS fields before saving.</p></section>;
}

function CloseTripForm({ details }: Readonly<{ details: TripAuthorityDetails }>) {
  if (details.trip.endOdometer !== null || details.trip.lockedForTransfer) return null;

  return <form action={closeTripAuthorityAction} className="vehicle-status-maintenance-panel"><div className="vehicle-form-section-header"><div><p className="eyebrow">Open trip authority</p><h2>Save and Close</h2><p>Enter each route end odometer. The server calculates the route distances and trip end odometer from the persisted legacy records.</p></div></div><input name="tripId" type="hidden" value={details.trip.tripId} /><input name="contractCode" type="hidden" value={details.trip.contractCode} /><input name="vmfCode" type="hidden" value={details.trip.vmfCode ?? ""} />{details.routes.length === 0 ? <div className="form-field"><label className="form-label" htmlFor="trip-end-odometer">End ODO Meter</label><input className="form-input" id="trip-end-odometer" min="0" name="endOdometer" type="number" required /></div> : <div className="vehicle-table-wrapper"><table className="vehicle-table"><caption className="sr-only">Enter route end odometers</caption><thead><tr><th scope="col">Route</th><th scope="col">Start ODO</th><th scope="col">End ODO</th></tr></thead><tbody>{details.routes.map((route) => <tr key={route.routeCode}><td><input name="routeCode" type="hidden" value={route.routeCode} />{route.routeCode}</td><td>{valueOrDash(route.startOdometer)}</td><td><label className="sr-only" htmlFor={`route-end-odometer-${route.routeCode}`}>End odometer for route {route.routeCode}</label><input className="form-input" id={`route-end-odometer-${route.routeCode}`} min={route.startOdometer ?? 0} name={`routeEndOdometer-${route.routeCode}`} type="number" defaultValue={route.endOdometer ?? ""} required /></td></tr>)}</tbody></table></div>}<div className="button-row"><button className="button button-primary" name="intent" value="close" type="submit">Save and Close</button><button className="button button-secondary" name="intent" value="renew" type="submit">Save and Renew</button><Link className="button button-secondary" href={`/trips/show?tripId=${details.trip.tripId}&tab=routes`}>Review routes</Link></div></form>;
}

function resultMessage(result: string) {
  switch (result) {
    case "closed":
      return { tone: "success", text: "Trip authority closed successfully." } as const;
    case "forbidden":
      return { tone: "error", text: "Your profile does not include Trip Authority access." } as const;
    case "validation":
      return { tone: "error", text: "Enter a valid end odometer for every route before closing the trip." } as const;
    case "missing-trip":
      return { tone: "error", text: "Choose a trip authority before opening this page." } as const;
    case "not-found":
      return { tone: "error", text: "The requested trip authority could not be found." } as const;
    case "unauthorized":
      return { tone: "error", text: "Your session is no longer authorized. Sign in again." } as const;
    case "unavailable":
      return { tone: "error", text: "The trip authority service is unavailable. Retry when the FIS API is available." } as const;
    case "error":
      return { tone: "error", text: "The trip authority could not be closed." } as const;
    default:
      return null;
  }
}

function unavailableMessage() {
  return <section className="vehicle-status-card" role="alert"><p className="eyebrow">API unavailable</p><h2>Trip authority details could not be loaded.</h2><p className="muted-copy">Retry when the FIS API is available.</p></section>;
}

export default async function ShowTripPage({ searchParams }: Readonly<{ searchParams: SearchParams }>) {
  await connection();
  const session = await getTripSession();
  const query = await searchParams;
  const requestedTripId = queryValue(query.tripId || query.TripID);
  const tripId = positiveInteger(requestedTripId);
  const returnPath = tripId ? `/trips/show?tripId=${tripId}` : "/trips/show";
  const sessionMessage = tripSessionMessage(session, returnPath);
  if (sessionMessage) return sessionMessage;
  if (session.status !== "authenticated") return unavailableMessage();
  if (!hasTripAuthorityAccess(session)) return tripAccessRestricted();
  if (tripId === null) return <main className="page-shell vehicle-page-shell"><section className="vehicle-status-card" role="alert"><p className="eyebrow">Trip authority not selected</p><h2>Choose a trip authority from the Trip Authority list.</h2><Link className="button button-primary" href="/trip-authorities">Trip Authority list</Link></section></main>;

  let details: TripAuthorityDetails;
  try {
    details = await getTripAuthorityDetails(tripId);
  } catch (error) {
    if (error instanceof TripAuthorityApiError && error.reason === "unauthorized") return <main className="page-shell vehicle-page-shell"><SessionRecovery returnPath={returnPath} /></main>;
    if (error instanceof TripAuthorityApiError && error.reason === "not-found") return <main className="page-shell vehicle-page-shell"><section className="vehicle-status-card" role="alert"><p className="eyebrow">Trip authority not found</p><h2>Trip authority {tripId} could not be found.</h2><Link className="button button-secondary" href="/trip-authorities">Back to Trip Authorities</Link></section></main>;
    console.error("FIS trip authority details failed", error instanceof Error ? error.message : "unknown error");
    return <main className="page-shell vehicle-page-shell">{unavailableMessage()}</main>;
  }

  const contractResult = details.trip.contractCode > 0
    ? await Promise.allSettled([getContract(details.trip.contractCode)])
    : [];
  const contract = contractResult[0]?.status === "fulfilled" ? contractResult[0].value : null;
  const vehicleCode = contract?.vmfCode ?? details.trip.vmfCode;
  const vehicle = vehicleCode
    ? await getVehicleForStatus(vehicleCode).catch(() => null)
    : null;
  let site: SiteRecord | null = null;
  let department: DepartmentRecord | null = null;
  if (contract?.siteCode) {
    const siteResult = await getSite(contract.siteCode).catch(() => null);
    site = siteResult;
    if (site?.departmentCode) department = await getDepartment(site.departmentCode).catch(() => null);
  }

  const activeTab = selectedTab(queryValue(query.tab));
  const message = resultMessage(queryValue(query.result));
  const status = details.trip.endOdometer === null ? "Open" : "Closed";

  return <main className="page-shell vehicle-page-shell"><article className="vehicle-card" aria-labelledby="show-trip-title"><header className="vehicle-page-header"><div><p className="eyebrow">Trip Authority No: {details.trip.tripId}</p><h1 id="show-trip-title">Trip Authority {details.trip.tripId}</h1><p>Trip Status: <strong>{status}</strong></p></div><div className="button-row"><Link className="button button-secondary" href="/trip-authorities">Back to Trips</Link><PrintButton label="Print Trip Authority" /><PrintButton label="Print Invoice" /></div></header>{message ? <div className={`notice notice-${message.tone}`} role={message.tone === "error" ? "alert" : "status"}>{message.text}</div> : null}{details.trip.lockedForTransfer ? <div className="notice notice-error" role="alert">This trip is locked for transfer and cannot be modified.</div> : null}<section className="vehicle-overview" aria-labelledby="trip-overview-title"><p className="eyebrow">Trip record</p><h2 id="trip-overview-title">Trip Authority No: {details.trip.tripId} has the following information</h2><TripTabs tripId={details.trip.tripId} activeTab={activeTab} />{activeTab === "vehicle" ? <VehicleInformation details={details} contract={contract} vehicle={vehicle} site={site} department={department} /> : activeTab === "trip" ? <TripInformation details={details} contract={contract} /> : activeTab === "drivers" ? <DriverInformation details={details} /> : activeTab === "passengers" ? <PassengerInformation details={details} /> : <RouteInformation details={details} />}</section><CloseTripForm details={details} /><div className="vehicle-footer-actions"><Link className="button button-secondary" href="/trip-authorities">Back to Trips</Link><Link className="button button-secondary" href="/home">Home</Link></div></article></main>;
}

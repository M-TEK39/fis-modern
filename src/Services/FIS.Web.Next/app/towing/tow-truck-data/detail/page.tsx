import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import { deleteTowTruckAction, saveTowTruckAction } from "@/app/towing/actions";
import SessionRecovery from "@/app/home/session-recovery";
import { getSession } from "@/lib/session";
import { getTowTrucks, TowingApiError, type TowTruckRecord } from "@/lib/api-towing";

const TOWING_ROLE = "Towing";
export type TowTruckDetailPageProps = { searchParams: Promise<Record<string, string | string[] | undefined>>; routePath?: string };
function getQueryValue(value: string | string[] | undefined) { return Array.isArray(value) ? value[0] : value; }
function getPositiveInteger(value: string | undefined) { const parsed = Number(value); return value && Number.isInteger(parsed) && parsed > 0 ? parsed : null; }
function hasTowingRole(roles: readonly string[]) { return roles.some((role) => role.localeCompare(TOWING_ROLE, undefined, { sensitivity: "accent" }) === 0); }

function TowTruckForm({ truck, name, routePath }: Readonly<{ truck: TowTruckRecord | null; name: string; routePath: string }>) {
  return <form action={saveTowTruckAction} className="vehicle-status-maintenance-panel">{truck ? <input name="towCode" type="hidden" value={truck.towCode} /> : null}<input name="returnPath" type="hidden" value={routePath} /><div className="vehicle-form-section-header"><div><p className="eyebrow">{truck ? "Existing record" : "New record"}</p><h2>{truck ? `Edit Tow Truck #${truck.towCode}` : "Capture Tow Truck Information"}</h2></div></div><div className="form-grid"><div className="form-field form-group-full"><label className="form-label" htmlFor="tow-name">Name</label><input className="form-input" id="tow-name" name="towName" maxLength={30} defaultValue={truck?.name ?? name} required /></div><div className="form-field form-group-full"><label className="form-label" htmlFor="tow-area">Area Operate</label><input className="form-input" id="tow-area" name="towArea" maxLength={255} defaultValue={truck?.area ?? ""} /></div><div className="form-field"><label className="form-label" htmlFor="tow-tel">Cell / Tel</label><input className="form-input" id="tow-tel" name="towTel" maxLength={50} defaultValue={truck?.telephone ?? ""} /></div><div className="form-field"><label className="form-label" htmlFor="tow-fax">Fax</label><input className="form-input" id="tow-fax" name="towFax" maxLength={50} defaultValue={truck?.fax ?? ""} /></div></div><div className="button-row"><button className="button button-primary" type="submit">{truck ? "Update" : "Submit"}</button><Link className="button button-secondary" href="/towing/tow-truck-data">Cancel</Link></div></form>;
}

function DeleteForm({ truck }: Readonly<{ truck: TowTruckRecord }>) {
  return <form action={deleteTowTruckAction} className="vehicle-status-maintenance-panel"><input name="towCode" type="hidden" value={truck.towCode} /><input name="returnPath" type="hidden" value="/towing/tow-truck-data" /><div className="vehicle-form-section-header"><div><p className="eyebrow">Legacy delete workflow</p><h2>Delete this assistance firm?</h2><p>The original Tow_Truck record will be removed, or marked deleted when the expanded audit fields are available.</p></div></div><div className="button-row"><button className="button button-danger" type="submit">DELETE</button><Link className="button button-secondary" href="/towing/tow-truck-data">Do NOT Delete</Link></div></form>;
}

export default async function TowTruckDetailPage({ searchParams, routePath = "/towing/tow-truck-data/detail" }: TowTruckDetailPageProps) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired") return <main className="page-shell vehicle-page-shell"><SessionRecovery returnPath={routePath} /></main>;
  if (session.status === "unavailable") return <main className="page-shell vehicle-page-shell"><section className="vehicle-status-card" role="alert"><p className="eyebrow">API unavailable</p><h2>Tow truck details could not be loaded.</h2></section></main>;
  if (!hasTowingRole(session.roles)) return <main className="page-shell vehicle-page-shell"><section className="vehicle-status-card" role="alert"><p className="eyebrow">Access restricted</p><h2>You do not have permission to maintain tow truck data.</h2></section></main>;
  const query = await searchParams;
  const towCode = getPositiveInteger(getQueryValue(query.towId) ?? getQueryValue(query.Acode));
  const name = (getQueryValue(query.towName) ?? getQueryValue(query.Aname) ?? "").trim().slice(0, 30);
  try {
    const trucks = await getTowTrucks();
    const truck = towCode ? trucks.find((item) => item.towCode === towCode) ?? null : null;
    if (towCode && !truck) return <main className="page-shell vehicle-page-shell"><section className="vehicle-status-card" role="alert"><p className="eyebrow">Record not found</p><h2>The requested tow truck was not found.</h2><Link className="button button-secondary" href="/towing/tow-truck-data">Back to Tow Truck Data</Link></section></main>;
    const confirmDelete = getQueryValue(query.confirmDelete) === "1";
    return <main className="page-shell vehicle-page-shell"><section className="vehicle-card" aria-labelledby="tow-truck-detail-title"><header className="vehicle-page-header"><div><p className="eyebrow">Road Side Assistance</p><h1 id="tow-truck-detail-title">Tow Truck Information Maintenance</h1><p>Capture or update the legacy Tow_Truck fields.</p></div><Link className="button button-secondary" href="/towing/tow-truck-data">Tow Truck Search</Link></header><TowTruckForm truck={truck} name={name} routePath="/towing/tow-truck-data" />{truck && confirmDelete ? <DeleteForm truck={truck} /> : truck ? <div className="button-row"><Link className="button button-danger" href={`/towing/tow-truck-data/detail?towId=${truck.towCode}&confirmDelete=1`}>Delete</Link></div> : null}</section></main>;
  } catch (error) {
    if (error instanceof TowingApiError && error.reason === "unauthorized") return <main className="page-shell vehicle-page-shell"><SessionRecovery returnPath={routePath} /></main>;
    console.error("FIS tow truck detail request failed", error instanceof Error ? error.message : "unknown error");
    return <main className="page-shell vehicle-page-shell"><section className="vehicle-status-card" role="alert"><p className="eyebrow">API unavailable</p><h2>Tow truck details could not be loaded.</h2><Link className="button button-primary" href={routePath}>Try again</Link></section></main>;
  }
}

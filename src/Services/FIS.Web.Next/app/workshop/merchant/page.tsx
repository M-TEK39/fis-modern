import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import { deleteWorkshopMerchantAction, saveWorkshopMerchantAction } from "@/app/workshop/actions";
import SessionRecovery from "@/app/home/session-recovery";
import { getWorkshopMerchant, getWorkshopMerchants, WorkshopMerchantApiError, type WorkshopMerchantRecord } from "@/lib/api-workshop-merchant";
import { getSession } from "@/lib/session";

function queryValue(value: string | string[] | undefined) { return Array.isArray(value) ? value[0] : value; }
function valueOrEmpty(value: string | null | undefined) { return value ?? ""; }

function MerchantForm({ merchant }: Readonly<{ merchant: WorkshopMerchantRecord | null }>) {
  return <form className="vehicle-status-maintenance-panel" action={saveWorkshopMerchantAction}><input name="returnPath" type="hidden" value="/workshop/merchant" />{merchant ? <input name="merchantCode" type="hidden" value={merchant.merchantCode} /> : null}<div className="vehicle-form-section-header"><div><p className="eyebrow">{merchant ? "Existing merchant" : "New merchant"}</p><h2>{merchant ? `Edit Merchant #${merchant.merchantCode}` : "Add Merchant"}</h2></div></div><div className="form-grid"><div className="form-field"><label className="form-label" htmlFor="workshop-merchant-name">Merchant Name</label><input className="form-input" id="workshop-merchant-name" name="merchantName" maxLength={40} defaultValue={valueOrEmpty(merchant?.name)} required /></div><div className="form-field"><label className="form-label" htmlFor="workshop-merchant-tel">Telephone</label><input className="form-input" id="workshop-merchant-tel" name="merchantTel" maxLength={30} defaultValue={valueOrEmpty(merchant?.tel)} /></div><div className="form-field"><label className="form-label" htmlFor="workshop-merchant-fax">Fax</label><input className="form-input" id="workshop-merchant-fax" name="merchantFax" maxLength={25} defaultValue={valueOrEmpty(merchant?.fax)} /></div><div className="form-field"><label className="form-label" htmlFor="workshop-merchant-email">Email</label><input className="form-input" id="workshop-merchant-email" name="merchantEmail" maxLength={25} type="email" defaultValue={valueOrEmpty(merchant?.email)} /></div></div><div className="button-row"><button className="button button-primary" type="submit">{merchant ? "Update" : "Add"}</button><Link className="button button-secondary" href="/workshop/merchant">{merchant ? "Cancel" : "Clear"}</Link></div></form>;
}

export default async function WorkshopMerchantPage({ searchParams }: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired" || session.status === "unavailable") return <main className="page-shell vehicle-page-shell"><SessionRecovery returnPath="/workshop/merchant" /></main>;
  if (!session.roles.some((role) => role.localeCompare("Workshop", undefined, { sensitivity: "accent" }) === 0)) return <main className="page-shell vehicle-page-shell"><section className="vehicle-status-card" role="alert"><h2>Access restricted.</h2></section></main>;
  const query = await searchParams;
  const search = queryValue(query.search) ?? "";
  const selectedCode = Number(queryValue(query.merchantCode));
  const saved = queryValue(query.saved) === "1";
  const updated = queryValue(query.updated) === "1";
  const deleted = queryValue(query.deleted) === "1";
  const errorMessage = queryValue(query.error);
  try {
    const merchants = await getWorkshopMerchants();
    const filtered = search.trim() ? merchants.filter((merchant) => merchant.name?.toLocaleLowerCase().includes(search.trim().toLocaleLowerCase())) : merchants;
    const selected = Number.isInteger(selectedCode) && selectedCode > 0 ? await getWorkshopMerchant(selectedCode).catch(() => null) : null;
    return <main className="page-shell vehicle-page-shell"><section className="vehicle-card" aria-labelledby="workshop-merchant-title"><header className="vehicle-page-header"><div><p className="eyebrow">Workshop maintenance</p><h1 id="workshop-merchant-title">Merchant Info Maintenance</h1><p>Search and update the merchants used by Workshop.</p></div><Link className="button button-secondary" href="/workshop">Workshop Menu</Link></header>{saved ? <div className="notice notice-success" role="status">Merchant saved successfully.</div> : null}{updated ? <div className="notice notice-success" role="status">Merchant updated successfully.</div> : null}{deleted ? <div className="notice notice-success" role="status">Merchant deleted successfully.</div> : null}{errorMessage ? <div className="notice notice-error" role="alert">{errorMessage}</div> : null}<form className="vehicle-status-maintenance-panel" method="get"><div className="vehicle-search-row"><label className="sr-only" htmlFor="workshop-merchant-search">Merchant name</label><input className="vehicle-search" id="workshop-merchant-search" name="search" defaultValue={search} placeholder="Search by merchant name" /><button className="button button-primary" type="submit">Search</button><Link className="button button-secondary" href="/workshop/merchant">Clear</Link></div></form><MerchantForm merchant={selected} /><section className="vehicle-status-maintenance-panel" aria-labelledby="workshop-merchants-list"><div className="vehicle-form-section-header"><div><p className="eyebrow">{filtered.length} merchant{filtered.length === 1 ? "" : "s"}</p><h2 id="workshop-merchants-list">Existing Merchants</h2></div></div>{filtered.length === 0 ? <p className="muted-copy">No merchants found.</p> : <div className="vehicle-table-wrapper"><table className="vehicle-table"><caption className="sr-only">Workshop merchants</caption><thead><tr><th scope="col">Merchant ID</th><th scope="col">Name</th><th scope="col">Telephone</th><th scope="col">Fax</th><th scope="col">Actions</th></tr></thead><tbody>{filtered.map((merchant) => <tr key={merchant.merchantCode}><td>{merchant.merchantCode}</td><td>{merchant.name || "-"}</td><td>{merchant.tel || "-"}</td><td>{merchant.fax || "-"}</td><td><div className="button-row"><Link className="button button-secondary button-small" href={`/workshop/merchant?merchantCode=${merchant.merchantCode}`}>Edit</Link><form action={deleteWorkshopMerchantAction}><input name="returnPath" type="hidden" value="/workshop/merchant" /><input name="merchantCode" type="hidden" value={merchant.merchantCode} /><button className="button button-danger button-small" type="submit">Delete</button></form></div></td></tr>)}</tbody></table></div>}</section></section></main>;
  } catch (error) {
    return <main className="page-shell vehicle-page-shell"><section className="vehicle-status-card" role="alert"><h2>{error instanceof WorkshopMerchantApiError && error.reason === "unavailable" ? "The workshop merchant service is temporarily unavailable." : "Workshop merchants could not be loaded."}</h2><Link className="button button-secondary" href="/workshop">Back</Link></section></main>;
  }
}

import Link from "next/link";
import { connection } from "next/server";
import { redirect } from "next/navigation";

import { logoutAction } from "@/app/actions/auth";
import { hasVehicleManagementPermission } from "@/app/drivers/access";
import SessionRecovery from "@/app/home/session-recovery";
import { createExtraCodeAction } from "@/app/validation-data/extras/actions";
import ExtraCodeForm from "@/app/validation-data/extras/extra-code-form";
import { ExtraCodeApiError, getExtraCodes, type ExtraCodeRecord } from "@/lib/api-extra-codes";
import { getSession } from "@/lib/session";

type ExtraCodeListPageProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
  routePath?: string;
};

function getQueryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

function valueOrDash(value: string | number | null | undefined) {
  return value === null || value === undefined || String(value).trim() === "" ? "-" : String(value);
}

function ErrorCard({ message, routePath = "/validation-data/extras" }: Readonly<{ message: string; routePath?: string }>) {
  return <section className="vehicle-status-card" role="alert"><p className="eyebrow">Access restricted</p><h2>{message}</h2><div className="button-row"><Link className="button button-primary" href={routePath}>Try again</Link><Link className="button button-secondary" href="/validation-data">Validation Data</Link></div></section>;
}

function ExtraCodeTable({ extras }: Readonly<{ extras: ExtraCodeRecord[] }>) {
  if (extras.length === 0) {
    return <div className="empty-state"><h2>No extras found</h2><p>Add an extra using the same legacy validation workflow.</p></div>;
  }

  return <div className="table-container"><div className="table-header"><span className="table-title">{extras.length} extra{extras.length === 1 ? "" : "s"}</span></div><div className="table-wrapper"><table className="data-table"><caption className="sr-only">Optional extras</caption><thead><tr><th scope="col">Extra code</th><th scope="col">Extra description</th><th scope="col">Actions</th></tr></thead><tbody>{extras.map((extra) => <tr key={extra.extraCode}><td>{extra.extraCode}</td><td>{valueOrDash(extra.description)}</td><td className="actions-column"><div className="table-actions"><Link className="button button-secondary button-small" href={`/validation-data/extras/delete?code=${encodeURIComponent(String(extra.extraCode))}`}>Delete</Link></div></td></tr>)}</tbody></table></div></div>;
}

export default async function ExtraCodeListPage({ searchParams, routePath = "/validation-data/extras" }: ExtraCodeListPageProps) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired") return <main className="page-shell vehicle-page-shell"><SessionRecovery returnPath={routePath} /></main>;
  if (session.status === "unavailable") return <main className="page-shell vehicle-page-shell"><ErrorCard message="Extra code maintenance is temporarily unavailable." routePath={routePath} /></main>;
  if (!hasVehicleManagementPermission(session.accessLevel)) return <main className="page-shell vehicle-page-shell"><ErrorCard message="You do not have permission to maintain extras." routePath="/home" /></main>;

  const query = await searchParams;
  const saved = getQueryValue(query.saved);
  const error = getQueryValue(query.error);
  try {
    const extras = await getExtraCodes();
    const notice = saved === "created" ? "Extra added successfully." : saved === "deleted" ? "Extra deleted successfully." : error;
    return <main className="page-shell vehicle-page-shell"><section className="vehicle-card" aria-labelledby="extra-code-list-title"><header className="vehicle-page-header"><div><p className="eyebrow">Validation / Vehicle</p><h1 id="extra-code-list-title">Optional Extras Maintenance</h1><p>Maintain the optional extras used by vehicle data and job-card workflows.</p></div><div className="button-row"><Link className="button button-secondary" href="/validation-data">Validation Data</Link></div></header>{notice ? <div className={`notice ${error ? "notice-error" : "notice-success"}`} role={error ? "alert" : "status"}>{notice}</div> : null}<ExtraCodeTable extras={extras} /><ExtraCodeForm action={createExtraCodeAction} /><div className="vehicle-footer-actions"><Link className="button button-secondary" href="/home">Home</Link><form action={logoutAction}><button className="button button-secondary" type="submit">Sign out</button></form></div></section></main>;
  } catch (caughtError) {
    if (caughtError instanceof ExtraCodeApiError && caughtError.reason === "unauthorized") return <main className="page-shell vehicle-page-shell"><SessionRecovery returnPath={routePath} /></main>;
    console.error("FIS extra code list request failed", caughtError instanceof Error ? caughtError.message : "unknown error");
    return <main className="page-shell vehicle-page-shell"><ErrorCard message="Extra code data could not be loaded." routePath={routePath} /></main>;
  }
}

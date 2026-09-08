import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import { FinanceFrame, FinanceRestricted, FinanceUnavailable, hasHeadOfficeFinanceAccess } from "@/app/finance/_components";
import { getSession } from "@/lib/session";

import { importStandardBankAction } from "./actions";

type Query = Record<string, string | string[] | undefined>;

function queryValue(query: Query, name: string) {
  const value = query[name];
  return Array.isArray(value) ? value[0] ?? "" : value ?? "";
}

export default async function StandardBankImportPage({ searchParams }: Readonly<{ searchParams: Promise<Query> }>) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status !== "authenticated") return <FinanceFrame title="Import Standard Bank Transactions" description="Import monthly Standard Bank transaction files."><FinanceUnavailable message="The sign-in service is temporarily unavailable. Please try again." /></FinanceFrame>;
  if (!hasHeadOfficeFinanceAccess(session.siteCode, session.email, session.roles)) return <FinanceFrame title="Import Standard Bank Transactions" description="Import monthly Standard Bank transaction files."><FinanceRestricted message="Standard Bank imports are limited to the Finance head-office workflow." /></FinanceFrame>;

  const query = await searchParams;
  const result = queryValue(query, "result");
  const message = queryValue(query, "message");
  const error = result === "error" || result === "forbidden";
  return <FinanceFrame title="Import Standard Bank Transactions" description="Import monthly Standard Bank transaction files.">
    {message ? <div className={`notice ${error ? "notice-error" : "notice-success"}`} role={error ? "alert" : "status"}>{message}</div> : null}
    <section className="vehicle-status-maintenance-panel">
      <div className="vehicle-form-section-header"><div><p className="eyebrow">Wesbank fuel card import</p><h2>Upload Standard Bank Transactions File</h2></div></div>
      <div className="notice notice-info" role="note">Download the latest DBF file from Wesbank, save it as CSV, then upload the CSV file.</div>
      <form action={importStandardBankAction} encType="multipart/form-data">
        <div className="form-grid"><div className="form-field"><label className="form-label" htmlFor="standard-bank-file">CSV file</label><input className="form-input" id="standard-bank-file" name="file" type="file" accept=".csv,text/csv" required /></div></div>
        <div className="button-row"><button className="button button-primary" type="submit">Upload File</button><Link className="button button-secondary" href="/finance">Finance Menu</Link></div>
      </form>
    </section>
  </FinanceFrame>;
}

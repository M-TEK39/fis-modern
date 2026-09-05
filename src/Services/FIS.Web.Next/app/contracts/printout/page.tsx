import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import PrintButton from "@/app/contracts/print-button";
import SessionRecovery from "@/app/home/session-recovery";
import { ContractApiError, getContract } from "@/lib/api-contracts";
import { getSession } from "@/lib/session";

export type ContractPrintoutPageProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
};

function getQueryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

function positiveInt(value: string | undefined) {
  const parsed = Number(value);
  return value && Number.isInteger(parsed) && parsed > 0 ? parsed : null;
}

function valueOrDash(value: string | number | null | undefined) {
  return value === null || value === undefined || String(value).trim() === "" ? "-" : String(value);
}

function formatDate(value: string | null) {
  return value?.slice(0, 10) || "-";
}

export default async function ContractPrintoutPage({ searchParams }: ContractPrintoutPageProps) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired") return <main className="page-shell vehicle-page-shell"><SessionRecovery returnPath="/contracts/printout" /></main>;
  if (session.status !== "authenticated") return <main className="page-shell vehicle-page-shell"><section className="vehicle-status-card" role="alert"><p className="eyebrow">API unavailable</p><h2>Contract printout could not be loaded.</h2></section></main>;

  const contractId = positiveInt(getQueryValue((await searchParams).contractId));
  if (!contractId) return <main className="page-shell vehicle-page-shell"><section className="vehicle-status-card" role="alert"><p className="eyebrow">Contract not selected</p><h2>Choose a contract from the print menu.</h2><Link className="button button-secondary" href="/contracts/print-menu">Print menu</Link></section></main>;

  try {
    const contract = await getContract(contractId);
    return <main className="page-shell vehicle-page-shell"><article className="vehicle-card contract-printout" aria-labelledby="contract-printout-title"><header className="vehicle-page-header"><div><p className="eyebrow">Fleet Management</p><h1 id="contract-printout-title">Vehicle Contract #{contract.contractCode}</h1><p>{valueOrDash(contract.fleetNumber)} / {valueOrDash(contract.registrationNumber)}</p></div><div className="button-row"><Link className="button button-secondary" href={`/contracts/detail?contractId=${contract.contractCode}`}>Back</Link><PrintButton /></div></header><section className="vehicle-status-maintenance-panel"><h2>Contract record</h2><dl className="contract-facts">{[["Status", valueOrDash(contract.contractStatusCode)], ["Vehicle code", valueOrDash(contract.vmfCode)], ["Site", `${valueOrDash(contract.siteDescription)} (${contract.siteCode})`], ["Driver", valueOrDash(contract.driverName)], ["Driver ID", valueOrDash(contract.driverId)], ["Start date", formatDate(contract.startDate)], ["End date", formatDate(contract.endDate)], ["Target return", formatDate(contract.targetReturnDate)], ["Start odometer", valueOrDash(contract.startOdometer)], ["End odometer", valueOrDash(contract.endOdometer)], ["Authorisation", valueOrDash(contract.authorisation)], ["Notes", valueOrDash(contract.notes)], ["Monthly km", valueOrDash(contract.monthlyKm)], ["Hours used", valueOrDash(contract.hoursUsed)], ["BAS fund", valueOrDash(contract.basFundCode)], ["BAS objective", valueOrDash(contract.basObjectiveCode)], ["BAS project", valueOrDash(contract.basProjectNumber)], ["BAS responsibility", valueOrDash(contract.basResponsibilityCode)]].map(([label, value]) => <div key={label}><dt>{label}</dt><dd>{value}</dd></div>)}</dl></section><div className="vehicle-footer-actions"><Link className="button button-secondary" href="/contracts/print-menu">Print menu</Link></div></article></main>;
  } catch (error) {
    if (error instanceof ContractApiError && error.reason === "unauthorized") return <main className="page-shell vehicle-page-shell"><SessionRecovery returnPath={`/contracts/printout?contractId=${contractId}`} /></main>;
    return <main className="page-shell vehicle-page-shell"><section className="vehicle-status-card" role="alert"><p className="eyebrow">Record unavailable</p><h2>Contract #{contractId} could not be loaded.</h2><Link className="button button-secondary" href="/contracts/print-menu">Print menu</Link></section></main>;
  }
}

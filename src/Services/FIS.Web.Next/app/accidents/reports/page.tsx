import Link from "next/link";
import { connection } from "next/server";
import { redirect } from "next/navigation";
import { Suspense } from "react";

import { logoutAction } from "@/app/actions/auth";
import SessionRecovery from "@/app/home/session-recovery";
import { getSession } from "@/lib/session";

const ACCIDENTS_ROLE = "Accidents";

const reportEntries = [
  { label: "1) One Vehicle Accidents", target: "Accident/RPT_one_num_main_accident.htm", href: "/accidents/reports/one-vehicle", available: true },
  { label: "2) Private Vehicle Accidents", target: "Accident/RPT_one_prnum_main_accident.htm", href: "/accidents/reports/private-vehicle", available: true },
  { label: "3) Private Vehicle Accidents (Capture under Description of Accident)", target: "Accident/RPT_one_prnumc_main_accident.htm", href: "/accidents/reports/private-vehicle/description", available: true },
  { label: "4) All Accidents - ALL DETAIL", target: "Accident/RPT_all_main_accident.htm", href: "/accidents/reports/all", available: true },
  { label: "5) All Accidents - GARAGE DETAIL", target: "Accident/RPT_allgar_main_accident.htm", href: "/accidents/reports/garage-detail", available: true },
  { label: "6) Report on New Accident's", target: "Accident/RPT_flagnew_main_accident.htm", href: "/accidents/reports/new-accidents", available: true },
  { label: "7) Report for a Department/Site, for a Period", target: "Accident/RPT_dept_period_main_accident.aspx", href: "/accidents/reports/department-period", available: true },
  { label: "8) Report for a Site, for a Period, for VIP/GG, for Hire Type", target: "Accident/RPT_dept_periodVIP_main_accident.aspx", href: "/accidents/reports/department-period-vip", available: true },
  { label: "9) Report for a Department/Site, for a Month/Year", target: "Accident/RPT_dept_month_main_accident.htm", href: "/accidents/reports/department-month", available: true },
  { label: "10) Report for a Department/Site, for a Book / Financial Year", target: "Accident/RPT_dept_finyear_main_accident.htm", href: "/accidents/reports/department-finyear", available: true },
  { label: "11) Report on Accident Costs for a Financial Year", target: "Accident/RPT_dept_finyear_cost_main_accident.aspx", href: "/accidents/reports/accident-costs-finyear", available: true },
  { label: "12) LETTER for Outstanding Accident documents", target: "Accident/RPT_letter_outstanddoc_main_accident.aspx", href: "/accidents/reports/outstanding-docs", available: true },
  { label: "13) LETTER for Inspection", target: "Accident/RPT_letter_inspection_main_accident.aspx", href: "/accidents/reports/inspection", available: true },
  { label: "14) List of Accident Categories", target: "Accident/RPT_categories_accident.aspx", href: "/accidents/reports/categories", available: true },
  { label: "15) Last GG Reference Number Used", target: "Accident/RPT_lastggref_accident.aspx", href: "/accidents/reports/last-gg-reference", available: true },
  { label: "16) Weekly, Quaterly And Yearly Report - Opened And Closed Accidents", target: "Accident/RPT_period_report1.aspx", href: "/accidents/reports/period", available: true },
  { label: "17) Report By Driver Name Or ID Number", target: "Accident/RPT_accident_driver_main.aspx", href: "/accidents/reports/driver", available: true },
  { label: "18) Report on ALL Duplicate Accidents", target: "Accident/RPT_F_dup_main_Accident.htm", href: "/accidents/reports/duplicate-accidents", available: true },
] as const;

function hasRole(roles: readonly string[], role: string) {
  return roles.some((candidate) => candidate.localeCompare(role, undefined, { sensitivity: "accent" }) === 0);
}

function LoadingState() {
  return <div className="loading-card" aria-busy="true"><span className="spinner" aria-hidden="true" /><p>Checking accident report access...</p></div>;
}

async function AccidentReportsContent() {
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired") return <SessionRecovery returnPath="/accidents/reports" />;
  if (session.status === "unavailable") {
    return <section className="vehicle-status-card" role="alert"><p className="eyebrow">API unavailable</p><h2>Your session could not be checked.</h2><p className="muted-copy">Retry when the FIS API is available.</p><Link className="button button-primary" href="/accidents/reports">Try again</Link></section>;
  }
  if (!hasRole(session.roles, ACCIDENTS_ROLE)) {
    return <section className="vehicle-status-card" role="alert"><p className="eyebrow">Access restricted</p><h2>You do not have permission to access accident reports.</h2></section>;
  }

  return (
    <>
      <div className="vehicle-menu-tiles">
        {reportEntries.map((entry) => (
          <section className="vehicle-menu-tile" key={entry.target}>
            {entry.available ? (
              <Link className="vehicle-menu-link" href={entry.href}>{entry.label}</Link>
            ) : (
              <div className="vehicle-menu-link vehicle-menu-link-disabled" aria-disabled="true">
                <span>{entry.label}</span>
                <small>Legacy source pending</small>
              </div>
            )}
          </section>
        ))}
      </div>
      <div className="vehicle-footer-actions">
        <Link className="button button-secondary" href="/accidents">Accident Menu</Link>
        <Link className="button button-secondary" href="/home">Home</Link>
        <form action={logoutAction}><button className="button button-secondary" type="submit">Sign out</button></form>
      </div>
    </>
  );
}

export default async function AccidentReportsPage() {
  await connection();
  return <main className="page-shell vehicle-page-shell"><section className="vehicle-card" aria-labelledby="accident-reports-title"><header className="vehicle-page-header"><div><p className="eyebrow">Fleet operations</p><h1 id="accident-reports-title">Accident Reports</h1><p>Run the available accident reports and track the remaining legacy report references.</p></div><Link className="button button-secondary" href="/accidents">Accident Menu</Link></header><Suspense fallback={<LoadingState />}><AccidentReportsContent /></Suspense></section></main>;
}

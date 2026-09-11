import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";
import { Suspense } from "react";

import { AccessRestricted, ReportsUnavailable } from "@/app/(fleet-operations)/reports/_components";
import { hasReportsRole } from "@/app/(fleet-operations)/reports/_utils";
import { getReportHelp, LegacyReportApiError } from "@/lib/api/reports/api-legacy-reports";
import { getSession } from "@/lib/auth/session";

const FALLBACK_HELP = [
  [
    "Report Navigation",
    "Use Reports Maintenance Menu for quick report shortcuts and FIS Report Menu for full module-specific reporting.",
  ],
  ["Date Ranges", "When supplying date filters, use From and To values in chronological order."],
  [
    "Vehicle Identifiers",
    "Most reports accept GG Number, GP Registration Number, VMF code, engine number, or VIN/chassis.",
  ],
  [
    "Compatibility",
    "Report results continue to use the legacy-compatible API definitions and fall back when expanded database objects are absent.",
  ],
] as const;

function HelpFallback() {
  return (
    <div className="loading-card" aria-busy="true">
      <span className="spinner" aria-hidden="true" />
      <p>Loading help…</p>
    </div>
  );
}

async function ReportsHelpContent() {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status !== "authenticated") return <ReportsUnavailable />;
  if (!hasReportsRole(session.roles)) return <AccessRestricted />;

  let sections: Array<{ title: string; content: string }> = FALLBACK_HELP.map(
    ([title, content]) => ({ title, content }),
  );
  try {
    const help = await getReportHelp();
    if (help.sections.length > 0) sections = help.sections;
  } catch (error) {
    if (!(error instanceof LegacyReportApiError)) throw error;
  }

  return (
    <div className="module-help-content">
      <section className="module-help-section" aria-labelledby="reports-help-purpose">
        <h2 id="reports-help-purpose">Purpose of Program</h2>
        <p className="module-help-intro">
          The purpose of the Reports program is to provide a user-friendly reporting system.
        </p>
      </section>

      <section className="module-help-section" aria-labelledby="reports-help-guidance">
        <h2 id="reports-help-guidance">Reference guidance</h2>
        <p>
          The report fields are mostly self-explanatory. Use the guidance below with the report menu
          that matches the information you need.
        </p>
        <p className="module-help-note">
          It is assumed that each user of the GGMT administrative functions on the Fleet Information
          System has received on-the-job training for the field relevant to their division.
        </p>
        {sections.map((section) => (
          <details className="module-help-disclosure" key={section.title} open>
            <summary>{section.title}</summary>
            <p>{section.content}</p>
          </details>
        ))}
      </section>

      <section className="module-help-section" aria-labelledby="reports-help-steps">
        <h2 id="reports-help-steps">How to use reports</h2>
        <ol className="module-help-steps">
          <li>Identify the reporting need.</li>
          <li>Select the main reporting option, such as FIS Reports.</li>
          <li>Select the specific field the report will pertain to.</li>
          <li>Enter the search criteria.</li>
          <li>Where necessary, enter the timeframe for the search.</li>
          <li>Submit the report criteria.</li>
          <li>Scrutinize the report.</li>
        </ol>
      </section>
      <div className="vehicle-footer-actions">
        <Link className="button button-secondary" href="/reports">
          Reports Menu
        </Link>
        <Link className="button button-secondary" href="/reports/fis-report">
          FIS Report Menu
        </Link>
      </div>
    </div>
  );
}

export default function ReportsHelpPage() {
  return (
    <main className="page-shell vehicle-page-shell">
      <article className="vehicle-card module-help-page" aria-labelledby="reports-help-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Fleet reports</p>
            <h1 id="reports-help-title">Reports Maintenance Information / Help</h1>
            <p>Reference guidance for report flows and usage.</p>
          </div>
          <Link className="button button-secondary" href="/reports">
            Reports Menu
          </Link>
        </header>
        <Suspense fallback={<HelpFallback />}>
          <ReportsHelpContent />
        </Suspense>
      </article>
    </main>
  );
}

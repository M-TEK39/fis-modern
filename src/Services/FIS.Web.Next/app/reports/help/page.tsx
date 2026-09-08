import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import { AccessRestricted, hasReportsRole, ReportsFrame, ReportsUnavailable } from "@/app/reports/_components";
import { getReportHelp, LegacyReportApiError } from "@/lib/api-legacy-reports";
import { getSession } from "@/lib/session";

const FALLBACK_HELP = [
  ["Report Navigation", "Use Reports Maintenance Menu for quick report shortcuts and FIS Report Menu for full module-specific reporting."],
  ["Date Ranges", "When supplying date filters, use From and To values in chronological order."],
  ["Vehicle Identifiers", "Most reports accept GG Number, GP Registration Number, VMF code, engine number, or VIN/chassis."],
  ["Compatibility", "Report results continue to use the legacy-compatible API definitions and fall back when expanded database objects are absent."],
] as const;

export default async function ReportsHelpPage() {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status !== "authenticated") return <ReportsFrame title="Reports Help" description="The reports workspace is temporarily unavailable."><ReportsUnavailable /></ReportsFrame>;
  if (!hasReportsRole(session.roles)) return <ReportsFrame title="Reports Help" description="Legacy report access is enforced on the server."><AccessRestricted /></ReportsFrame>;

  let sections: Array<{ title: string; content: string }> = FALLBACK_HELP.map(([title, content]) => ({ title, content }));
  try {
    const help = await getReportHelp();
    if (help.sections.length > 0) sections = help.sections;
  } catch (error) {
    if (!(error instanceof LegacyReportApiError)) throw error;
  }

  return (
    <ReportsFrame title="Reports Maintenance Information / Help" description="Reference guidance for report flows and usage.">
      <div className="vehicle-menu-tiles">
        {sections.map((section) => <section className="vehicle-status-maintenance-panel" key={section.title}><h2>{section.title}</h2><p className="muted-copy">{section.content}</p></section>)}
      </div>
      <div className="vehicle-footer-actions"><Link className="button button-secondary" href="/reports">Reports Menu</Link><Link className="button button-secondary" href="/reports/fis-report">FIS Report Menu</Link></div>
    </ReportsFrame>
  );
}

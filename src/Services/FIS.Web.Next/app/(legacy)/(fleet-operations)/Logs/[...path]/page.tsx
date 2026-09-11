import { type ReportQuery } from "@/app/(fleet-operations)/reports/_components";
import { ReportsRoutePage } from "@/app/(fleet-operations)/reports/[slug]/_route";
import { StreamedRoute } from "@/components/app-shell/streamed-route";

const REPORTS_BY_PATH: Record<string, string | undefined> = {
  "rpt_outstanding1.aspx": "logsheets-one-vehicle",
  "rpt_logs_per_vehicle.aspx": "logsheets-one-vehicle",
  "rpt_logs_per_vehicle_1.aspx": "logsheets-vehicle-odo-balance",
  "rpt_logsheet_per_vehicle.aspx": "vehicle-logs-report",
  "rpt_logsheet_per_rek.aspx": "logsheets-vehicle-details-per-rek",
  "rpt_uits_els_en_logs_geld_main.aspx": "logsheets-all-outstanding",
  "rpt_logs_per_dept_1.aspx": "logsheets",
};

async function LegacyLogsheetReportPageContent({
  params,
  searchParams,
}: Readonly<{ params: Promise<{ path: string[] }>; searchParams: Promise<ReportQuery> }>) {
  const { path } = await params;
  const reportKey = REPORTS_BY_PATH[path.join("/").toLowerCase()];
  if (!reportKey) return <ReportsRoutePage slug="logsheets" searchParams={searchParams} />;
  return (
    <ReportsRoutePage slug="logsheets" searchParams={searchParams} forcedReportKey={reportKey} />
  );
}

export default function LegacyLogsheetReportPage(
  props: Readonly<{
    params: Promise<{ path: string[] }>;
    searchParams: Promise<ReportQuery>;
  }>,
) {
  return (
    <StreamedRoute>
      <LegacyLogsheetReportPageContent {...props} />
    </StreamedRoute>
  );
}

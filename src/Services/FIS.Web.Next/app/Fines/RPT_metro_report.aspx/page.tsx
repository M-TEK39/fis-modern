import { FineReportPage } from "@/app/fines/reports/[mode]/page";

export default function LegacyMetroReportPage(props: Parameters<typeof FineReportPage>[0]) {
  return (
    <FineReportPage
      {...props}
      forcedMode="metro"
      legacyResult
      routePath="/Fines/RPT_metro_report.aspx"
    />
  );
}

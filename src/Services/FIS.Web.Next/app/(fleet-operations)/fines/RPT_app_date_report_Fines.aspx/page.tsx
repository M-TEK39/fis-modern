import { FineReportPage } from "@/app/(fleet-operations)/fines/reports/[mode]/page";

export default function LegacyAppearDateReportResultPage(
  props: Parameters<typeof FineReportPage>[0],
) {
  return (
    <FineReportPage
      {...props}
      forcedMode="appear-date"
      legacyResult
      routePath="/fines/RPT_app_date_report_Fines.aspx"
    />
  );
}

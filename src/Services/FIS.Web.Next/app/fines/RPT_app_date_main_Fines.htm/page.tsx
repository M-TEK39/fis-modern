import { FineReportPage } from "@/app/fines/reports/[mode]/page";

export default function LegacyAppearDateReportPage(props: Parameters<typeof FineReportPage>[0]) {
  return <FineReportPage {...props} forcedMode="appear-date" routePath="/fines/RPT_app_date_main_Fines.htm" />;
}

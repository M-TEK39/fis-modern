import { FineReportPage } from "@/app/fines/reports/[mode]/page";

export default function LegacyTrafficDeptReportPage(props: Parameters<typeof FineReportPage>[0]) {
  return <FineReportPage {...props} forcedMode="traffic-dept" legacyResult routePath="/fines/RPT_traffic_all_report.aspx" />;
}

import { FineReportPage } from "@/app/fines/reports/[mode]/page";

export default function LegacyAllFinesReportPage(props: Parameters<typeof FineReportPage>[0]) {
  return (
    <FineReportPage {...props} forcedMode="all" legacyResult routePath="/Fines/RPT_FinesAll.aspx" />
  );
}

import { FineReportPage } from "@/app/fines/reports/[mode]/page";

export default function LegacyMetroSelectionPage(props: Parameters<typeof FineReportPage>[0]) {
  return (
    <FineReportPage {...props} forcedMode="metro" routePath="/Fines/RPT_FINESPERMETRO1.aspx" />
  );
}

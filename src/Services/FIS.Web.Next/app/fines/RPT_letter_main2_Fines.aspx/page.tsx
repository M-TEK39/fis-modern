import { FineReportPage } from "@/app/fines/reports/[mode]/page";

export default function LegacyFineReissueSelectionPage(props: Parameters<typeof FineReportPage>[0]) {
  return <FineReportPage {...props} forcedMode="letter" legacyResult routePath="/fines/RPT_letter_main2_Fines.aspx" />;
}

import { FineReportPage } from "@/app/fines/reports/[mode]/page";

export default function LegacyFineLetterReportPage(props: Parameters<typeof FineReportPage>[0]) {
  return (
    <FineReportPage
      {...props}
      forcedMode="fine-detail"
      legacyResult
      routePath="/fines/RPT_letter_report_Fines.aspx"
    />
  );
}

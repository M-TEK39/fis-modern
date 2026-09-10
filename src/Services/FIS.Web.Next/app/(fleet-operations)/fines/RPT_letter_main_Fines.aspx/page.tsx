import { FineReportPage } from "@/app/(fleet-operations)/fines/reports/[mode]/page";

export default function LegacyFineReissueReportPage(props: Parameters<typeof FineReportPage>[0]) {
  return (
    <FineReportPage {...props} forcedMode="letter" routePath="/fines/RPT_letter_main_Fines.aspx" />
  );
}

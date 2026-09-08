import { FineReportPage } from "@/app/fines/reports/[mode]/page";

export default function LegacyOneVehicleReportResultPage(
  props: Parameters<typeof FineReportPage>[0],
) {
  return (
    <FineReportPage
      {...props}
      forcedMode="one-vehicle"
      legacyResult
      routePath="/fines/RPT_one_num_report_Fines.aspx"
    />
  );
}

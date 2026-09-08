import { FineReportPage } from "@/app/fines/reports/[mode]/page";

export default function LegacyVehiclePeriodReportResultPage(
  props: Parameters<typeof FineReportPage>[0],
) {
  return (
    <FineReportPage
      {...props}
      forcedMode="vehicle"
      legacyResult
      routePath="/Fines/RPT_finepervehicle_report.aspx"
    />
  );
}

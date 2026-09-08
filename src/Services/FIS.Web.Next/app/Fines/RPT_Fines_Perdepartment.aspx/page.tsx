import { FineReportPage } from "@/app/fines/reports/[mode]/page";

export default function LegacyVehiclePeriodReportPage(props: Parameters<typeof FineReportPage>[0]) {
  return (
    <FineReportPage
      {...props}
      forcedMode="vehicle"
      routePath="/Fines/RPT_Fines_Perdepartment.aspx"
    />
  );
}

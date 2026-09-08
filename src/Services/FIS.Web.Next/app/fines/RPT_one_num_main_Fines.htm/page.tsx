import { FineReportPage } from "@/app/fines/reports/[mode]/page";

export default function LegacyOneVehicleReportPage(props: Parameters<typeof FineReportPage>[0]) {
  return (
    <FineReportPage
      {...props}
      forcedMode="one-vehicle"
      routePath="/fines/RPT_one_num_main_Fines.htm"
    />
  );
}

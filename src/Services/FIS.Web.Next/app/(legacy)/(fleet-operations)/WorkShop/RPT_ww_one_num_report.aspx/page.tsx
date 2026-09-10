import { WorkshopReportPage } from "@/app/(fleet-operations)/workshop/reports/[mode]/page";

export default function LegacyWorkshopOneVehicleReportResult({
  searchParams,
}: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  return (
    <WorkshopReportPage
      mode="one-vehicle"
      searchParams={searchParams}
      routePath="/WorkShop/RPT_ww_one_num_report.aspx"
    />
  );
}

import { WorkshopReportPage } from "@/app/workshop/reports/[mode]/page";

export default function LegacyWorkshopOneVehicleReport({
  searchParams,
}: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  return (
    <WorkshopReportPage
      mode="one-vehicle"
      searchParams={searchParams}
      routePath="/WorkShop/RPT_WW_one_num_main.aspx"
    />
  );
}

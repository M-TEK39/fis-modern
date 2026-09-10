import { WorkshopReportPage } from "@/app/(fleet-operations)/workshop/reports/[mode]/page";

export default function LegacyWorkshopPeriodReport({
  searchParams,
}: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  return (
    <WorkshopReportPage
      mode="period"
      searchParams={searchParams}
      routePath="/WorkShop/RPT_ww_date_main.aspx"
    />
  );
}

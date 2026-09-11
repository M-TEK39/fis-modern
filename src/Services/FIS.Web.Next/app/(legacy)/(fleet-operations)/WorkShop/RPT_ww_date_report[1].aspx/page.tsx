import { WorkshopReportPage } from "@/app/(fleet-operations)/workshop/reports/[mode]/_route";

export default function LegacyWorkshopPeriodReportAlternate({
  searchParams,
}: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  return (
    <WorkshopReportPage
      mode="period"
      searchParams={searchParams}
      routePath="/WorkShop/RPT_ww_date_report[1].aspx"
    />
  );
}

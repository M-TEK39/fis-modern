import { WorkshopReportPage } from "@/app/(fleet-operations)/workshop/reports/[mode]/page";

export default function LegacyWorkshopPrintJobCardReport({
  searchParams,
}: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  return (
    <WorkshopReportPage
      mode="print-job-card"
      searchParams={searchParams}
      routePath="/WorkShop/RPT_ww_printjob_main.aspx"
    />
  );
}

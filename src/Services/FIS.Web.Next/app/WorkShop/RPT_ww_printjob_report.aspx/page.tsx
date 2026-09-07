import { WorkshopReportPage } from "@/app/workshop/reports/[mode]/page";

export default function LegacyWorkshopPrintJobCardReportResult({ searchParams }: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  return <WorkshopReportPage mode="print-job-card" searchParams={searchParams} routePath="/WorkShop/RPT_ww_printjob_report.aspx" />;
}

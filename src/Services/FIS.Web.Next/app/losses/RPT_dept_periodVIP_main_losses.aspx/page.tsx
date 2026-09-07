import LossReportPage, { type LossReportPageProps } from "@/app/losses/reports/[mode]/page";

export default function LegacyLossDepartmentPeriodReport({ searchParams }: Pick<LossReportPageProps, "searchParams">) {
  return <LossReportPage params={Promise.resolve({ mode: "dept-period" })} searchParams={searchParams} routePath="/losses/RPT_dept_periodVIP_main_losses.aspx" />;
}

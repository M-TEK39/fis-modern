import LossReportPage, { type LossReportPageProps } from "@/app/losses/reports/[mode]/page";

export default function LegacyLossReport({ params, searchParams }: LossReportPageProps) {
  return <LossReportPage params={params} searchParams={searchParams} routePath="/reports/losses" />;
}

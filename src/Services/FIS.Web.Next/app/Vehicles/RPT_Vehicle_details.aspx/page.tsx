import { type ReportQuery } from "@/app/reports/_components";
import { ReportsRoutePage } from "@/app/reports/[slug]/page";

export default function LegacyVehicleInfoReportPage({ searchParams }: Readonly<{ searchParams: Promise<ReportQuery> }>) {
  return <ReportsRoutePage slug="vehicle-info" searchParams={searchParams} />;
}

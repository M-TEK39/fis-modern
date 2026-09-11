import { type ReportQuery } from "@/app/(fleet-operations)/reports/_components";
import { ReportsRoutePage } from "@/app/(fleet-operations)/reports/[slug]/_route";

export default function LegacyVehicleInfoReportPage({
  searchParams,
}: Readonly<{ searchParams: Promise<ReportQuery> }>) {
  return <ReportsRoutePage slug="vehicle-info" searchParams={searchParams} />;
}

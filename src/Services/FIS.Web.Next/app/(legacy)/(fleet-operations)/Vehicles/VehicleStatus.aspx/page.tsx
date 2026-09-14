import { ReportsRoutePage } from "@/app/(fleet-operations)/reports/[slug]/_route";
import type { ReportQuery } from "@/app/(fleet-operations)/reports/_utils";

export default function LegacyVehicleStatusPage({
  searchParams,
}: Readonly<{ searchParams: Promise<ReportQuery> }>) {
  return <ReportsRoutePage slug="vehicle-status-range" searchParams={searchParams} />;
}

import { type ReportQuery } from "@/app/reports/_components";
import { ReportsRoutePage } from "@/app/reports/[slug]/page";

export default function LegacyVehicleReportsPage({ searchParams }: Readonly<{ searchParams: Promise<ReportQuery> }>) {
  return <ReportsRoutePage slug="vehicles" searchParams={searchParams} />;
}

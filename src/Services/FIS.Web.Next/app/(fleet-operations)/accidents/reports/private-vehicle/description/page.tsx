import { PrivateVehicleReportPage } from "@/app/(fleet-operations)/accidents/reports/private-vehicle/_route";

export default function PrivateVehicleDescriptionReportPage({
  searchParams,
}: {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}) {
  return <PrivateVehicleReportPage searchParams={searchParams} defaultMode="description" />;
}

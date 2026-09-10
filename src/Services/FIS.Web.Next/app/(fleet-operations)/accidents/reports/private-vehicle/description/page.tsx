import { PrivateVehicleReportPage } from "@/app/(fleet-operations)/accidents/reports/private-vehicle/page";

export default async function PrivateVehicleDescriptionReportPage({
  searchParams,
}: {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}) {
  return <PrivateVehicleReportPage searchParams={searchParams} defaultMode="description" />;
}

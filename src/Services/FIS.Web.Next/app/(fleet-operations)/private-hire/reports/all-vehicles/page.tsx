import PrivateHireReportPage from "@/app/(fleet-operations)/private-hire/reports/page";

export default function AllPrivateHireVehiclesReport({
  searchParams,
}: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  return (
    <PrivateHireReportPage
      searchParams={searchParams}
      kind="all"
      routePath="/private-hire/reports/all-vehicles"
    />
  );
}

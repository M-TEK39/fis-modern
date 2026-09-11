import PrivateHireReportPage from "@/app/(fleet-operations)/private-hire/reports/_route";

export default function PrivateHireVehiclesPerDepartmentAlias({
  searchParams,
}: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  return (
    <PrivateHireReportPage
      searchParams={searchParams}
      kind="department"
      routePath="/private-hire/reports/vehicles-per-department"
    />
  );
}

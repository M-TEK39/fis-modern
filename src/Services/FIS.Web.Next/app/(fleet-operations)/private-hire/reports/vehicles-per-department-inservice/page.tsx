import PrivateHireReportPage from "@/app/(fleet-operations)/private-hire/reports/_route";

export default function PrivateHireVehiclesPerDepartmentInServiceAlias({
  searchParams,
}: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  return (
    <PrivateHireReportPage
      searchParams={searchParams}
      kind="department-in-service"
      routePath="/private-hire/reports/vehicles-per-department-inservice"
    />
  );
}

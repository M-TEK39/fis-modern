import PrivateHireReportPage from "@/app/private-hire/reports/page";

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

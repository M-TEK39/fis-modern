import PrivateHireReportPage from "@/app/private-hire/reports/page";

export default function PrivateHireVehiclesPerSiteReport({
  searchParams,
}: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  return (
    <PrivateHireReportPage
      searchParams={searchParams}
      kind="site"
      routePath="/private-hire/reports/per-site"
    />
  );
}

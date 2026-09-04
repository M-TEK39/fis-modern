import RenumberedReportPage from "@/app/vehicles/renumbered-report/page";

type LegacyRenumberedReportPageProps = {
  searchParams: Promise<{ page?: string | string[] }>;
};

export default function LegacyRenumberedReportPage({ searchParams }: LegacyRenumberedReportPageProps) {
  return <RenumberedReportPage routePath="/Master-File/RPT_renumbered.aspx" searchParams={searchParams} />;
}

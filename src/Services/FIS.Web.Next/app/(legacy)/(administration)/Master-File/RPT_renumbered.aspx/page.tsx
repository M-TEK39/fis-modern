import RenumberedReportPage, {
  type RenumberedReportPageProps,
} from "@/app/(fleet-operations)/vehicles/renumbered-report/_route";

export default function LegacyRenumberedReportPage({
  searchParams,
}: Pick<RenumberedReportPageProps, "searchParams">) {
  return (
    <RenumberedReportPage
      routePath="/Master-File/RPT_renumbered.aspx"
      searchParams={searchParams}
    />
  );
}

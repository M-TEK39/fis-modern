import FineMaintenancePage, { type FineMaintenancePageProps } from "@/app/fines/maintenance/page";

export default function LegacyFineMaintenanceHistoricalPage(props: FineMaintenancePageProps) {
  return <FineMaintenancePage {...props} routePath="/fines/MNT_finej_getreg.aspx" />;
}

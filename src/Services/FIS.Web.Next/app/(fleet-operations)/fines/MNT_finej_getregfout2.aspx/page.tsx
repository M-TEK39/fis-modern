import FineMaintenancePage, {
  type FineMaintenancePageProps,
} from "@/app/(fleet-operations)/fines/maintenance/page";

export default function LegacyFineMaintenanceHistoricalResultPage(props: FineMaintenancePageProps) {
  return <FineMaintenancePage {...props} routePath="/fines/MNT_finej_getregfout2.aspx" />;
}

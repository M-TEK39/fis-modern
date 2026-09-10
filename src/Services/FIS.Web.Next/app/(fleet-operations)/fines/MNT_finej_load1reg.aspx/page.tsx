import FineMaintenancePage, {
  type FineMaintenancePageProps,
} from "@/app/(fleet-operations)/fines/maintenance/page";

export default function LegacyFineMaintenanceHistoricalLookupPage(props: FineMaintenancePageProps) {
  return <FineMaintenancePage {...props} routePath="/fines/MNT_finej_load1reg.aspx" />;
}

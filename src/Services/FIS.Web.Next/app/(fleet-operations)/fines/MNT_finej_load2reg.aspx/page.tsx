import FineMaintenancePage, {
  type FineMaintenancePageProps,
} from "@/app/(fleet-operations)/fines/maintenance/page";

export default function LegacyFineMaintenanceHistoricalLookupResultPage(
  props: FineMaintenancePageProps,
) {
  return <FineMaintenancePage {...props} routePath="/fines/MNT_finej_load2reg.aspx" />;
}

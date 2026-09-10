import FineMaintenancePage, {
  type FineMaintenancePageProps,
} from "@/app/(fleet-operations)/fines/maintenance/page";

export default function LegacyFineMaintenanceResultPage(props: FineMaintenancePageProps) {
  return <FineMaintenancePage {...props} routePath="/fines/MNT_fine_getregfout2.aspx" />;
}

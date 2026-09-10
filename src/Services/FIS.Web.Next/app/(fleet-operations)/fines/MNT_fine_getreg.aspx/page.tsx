import FineMaintenancePage, {
  type FineMaintenancePageProps,
} from "@/app/(fleet-operations)/fines/maintenance/page";

export default function LegacyFineMaintenancePage(props: FineMaintenancePageProps) {
  return <FineMaintenancePage {...props} routePath="/fines/MNT_fine_getreg.aspx" />;
}

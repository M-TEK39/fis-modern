import FineMaintenancePage, { type FineMaintenancePageProps } from "@/app/fines/maintenance/page";

export default function LegacyFineMaintenanceLookupPage(props: FineMaintenancePageProps) {
  return <FineMaintenancePage {...props} routePath="/fines/MNT_fine_load1reg.aspx" />;
}

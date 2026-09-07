import { PrivateHireMaintenancePage } from "@/app/private-hire/maintenance/page";

export default function LegacyPrivateHireVehicleDelete({ searchParams }: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  return <PrivateHireMaintenancePage searchParams={searchParams} forcedMode="delete" routePath="/Private_Hire/MNT_del_phv_pass.aspx" />;
}

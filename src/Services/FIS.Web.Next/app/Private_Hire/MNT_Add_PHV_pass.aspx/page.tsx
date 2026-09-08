import { PrivateHireMaintenancePage } from "@/app/private-hire/maintenance/page";

export default function LegacyPrivateHireVehicleAdd({
  searchParams,
}: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  return (
    <PrivateHireMaintenancePage
      searchParams={searchParams}
      forcedMode="add"
      routePath="/Private_Hire/MNT_Add_PHV_pass.aspx"
    />
  );
}

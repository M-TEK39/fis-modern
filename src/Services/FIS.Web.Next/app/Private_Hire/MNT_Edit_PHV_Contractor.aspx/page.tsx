import { PrivateHireMaintenancePage } from "@/app/private-hire/maintenance/page";

export default function LegacyPrivateHireContractorEdit({
  searchParams,
}: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  return (
    <PrivateHireMaintenancePage
      searchParams={searchParams}
      forcedMode="contractor-edit"
      routePath="/Private_Hire/MNT_Edit_PHV_Contractor.aspx"
    />
  );
}

import { PrivateHireMaintenancePage } from "@/app/(fleet-operations)/private-hire/maintenance/page";

export default function LegacyPrivateHireContractorAdd({
  searchParams,
}: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  return (
    <PrivateHireMaintenancePage
      searchParams={searchParams}
      forcedMode="contractor-add"
      routePath="/Private_Hire/Add_PHV_contractor.aspx"
    />
  );
}

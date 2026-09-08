import LossMaintenancePage from "@/app/losses/maintenance/page";

type LegacyLossMaintenanceProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
};

export default function LegacyLowerLossMaintenancePage({
  searchParams,
}: LegacyLossMaintenanceProps) {
  return (
    <LossMaintenancePage routePath="/losses/MNT_Loss_GetGg.aspx" searchParams={searchParams} />
  );
}

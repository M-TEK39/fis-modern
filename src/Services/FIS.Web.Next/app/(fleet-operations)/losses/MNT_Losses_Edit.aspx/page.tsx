import EditLossPage from "@/app/(fleet-operations)/losses/edit/_route";

type LegacyEditLossProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
};

export default function LegacyLowerEditLossPage({ searchParams }: LegacyEditLossProps) {
  return <EditLossPage routePath="/losses/MNT_Losses_Edit.aspx" searchParams={searchParams} />;
}

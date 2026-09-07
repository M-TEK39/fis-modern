import EditLossPage from "@/app/losses/edit/page";

type LegacyEditLossProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
};

export default function LegacyLowerEditLossPage({ searchParams }: LegacyEditLossProps) {
  return <EditLossPage routePath="/losses/MNT_Losses_Edit.aspx" searchParams={searchParams} />;
}

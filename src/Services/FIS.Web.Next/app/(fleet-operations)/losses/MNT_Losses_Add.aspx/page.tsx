import AddLossPage from "@/app/(fleet-operations)/losses/add/page";

type LegacyAddLossProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
};

export default function LegacyLowerAddLossPage({ searchParams }: LegacyAddLossProps) {
  return <AddLossPage routePath="/losses/MNT_Losses_Add.aspx" searchParams={searchParams} />;
}

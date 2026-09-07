import DeleteLossPage from "@/app/losses/delete/page";

type LegacyDeleteLossProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
};

export default function LegacyLowerDeleteLossPage({ searchParams }: LegacyDeleteLossProps) {
  return <DeleteLossPage routePath="/losses/MNT_Losses_Delete.aspx" searchParams={searchParams} />;
}

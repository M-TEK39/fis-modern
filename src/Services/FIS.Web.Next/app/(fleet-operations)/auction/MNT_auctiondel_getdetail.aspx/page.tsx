import AuctionDeleteDetailPage from "@/app/(fleet-operations)/auction/delete-vehicle/detail/page";

type LegacyAuctionDeleteDetailProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
};

export default function LegacyAuctionDeleteDetailPage({
  searchParams,
}: LegacyAuctionDeleteDetailProps) {
  return (
    <AuctionDeleteDetailPage
      routePath="/auction/MNT_auctiondel_getdetail.aspx"
      searchParams={searchParams}
    />
  );
}

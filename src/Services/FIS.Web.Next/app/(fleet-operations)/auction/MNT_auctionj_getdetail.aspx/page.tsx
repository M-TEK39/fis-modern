import AuctionDetailPage from "@/app/(fleet-operations)/auction/maintenance/detail/page";

type LegacyAuctionDetailProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
};

export default function LegacyAuctionDetailPage({ searchParams }: LegacyAuctionDetailProps) {
  return (
    <AuctionDetailPage
      routePath="/auction/MNT_auctionj_getdetail.aspx"
      searchParams={searchParams}
    />
  );
}

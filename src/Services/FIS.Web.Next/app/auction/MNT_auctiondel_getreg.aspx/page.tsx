import AuctionDeletePage from "@/app/auction/delete-vehicle/page";

type LegacyAuctionDeleteProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
};

export default function LegacyAuctionDeletePage({ searchParams }: LegacyAuctionDeleteProps) {
  return (
    <AuctionDeletePage
      routePath="/auction/MNT_auctiondel_getreg.aspx"
      searchParams={searchParams}
    />
  );
}

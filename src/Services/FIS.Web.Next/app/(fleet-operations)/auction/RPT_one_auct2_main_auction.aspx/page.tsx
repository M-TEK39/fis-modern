import AuctionReportPage from "@/app/(fleet-operations)/auction/reports/[mode]/page";

type LegacyAuctionReportProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
};

export default function LegacyAuctionByLotReportPage({ searchParams }: LegacyAuctionReportProps) {
  return (
    <AuctionReportPage
      routePath="/auction/RPT_one_auct2_main_auction.aspx"
      params={Promise.resolve({ mode: "auction-lot" })}
      searchParams={searchParams}
    />
  );
}

import AuctionReportPage from "@/app/auction/reports/[mode]/page";

type LegacyAuctionReportProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
};

export default function LegacyAuctionByGgReportPage({ searchParams }: LegacyAuctionReportProps) {
  return <AuctionReportPage routePath="/auction/RPT_one_auct1_main_auction.aspx" params={Promise.resolve({ mode: "auction-gg" })} searchParams={searchParams} />;
}

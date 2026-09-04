import AuctionReportPage from "@/app/auction/reports/[mode]/page";

type LegacyAuctionReportProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
};

export default function LegacySaleToNameAuctionReportPage({ searchParams }: LegacyAuctionReportProps) {
  return <AuctionReportPage routePath="/auction/RPT_one_name_main_auction.aspx" params={Promise.resolve({ mode: "sale-to-name" })} searchParams={searchParams} />;
}

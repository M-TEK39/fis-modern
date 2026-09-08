import AuctionReportPage from "@/app/auction/reports/[mode]/page";

type LegacyAuctionReportProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
};

export default function LegacyAllAuctionReportPage({ searchParams }: LegacyAuctionReportProps) {
  return (
    <AuctionReportPage
      routePath="/auction/RPT_all_main_auction.aspx"
      params={Promise.resolve({ mode: "all-vehicles" })}
      searchParams={searchParams}
    />
  );
}

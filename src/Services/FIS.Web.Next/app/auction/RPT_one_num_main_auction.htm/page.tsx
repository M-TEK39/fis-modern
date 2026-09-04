import AuctionReportPage from "@/app/auction/reports/[mode]/page";

type LegacyAuctionReportProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
};

export default function LegacyOneVehicleAuctionReportPage({ searchParams }: LegacyAuctionReportProps) {
  return <AuctionReportPage routePath="/auction/RPT_one_num_main_auction.htm" params={Promise.resolve({ mode: "one-vehicle" })} searchParams={searchParams} />;
}

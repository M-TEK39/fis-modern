import AuctionMaintenancePage from "@/app/auction/maintenance/page";

type LegacyAuctionMaintenanceProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
};

export default function LegacyAuctionMaintenancePage({
  searchParams,
}: LegacyAuctionMaintenanceProps) {
  return (
    <AuctionMaintenancePage
      routePath="/auction/MNT_auctionj_getreg.aspx"
      searchParams={searchParams}
    />
  );
}

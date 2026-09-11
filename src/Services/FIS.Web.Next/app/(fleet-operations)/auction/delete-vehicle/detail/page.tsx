import AuctionDeleteDetailRoute, { type AuctionDeleteDetailPageProps } from "./_route";

export default function AuctionDeleteDetailPage({
  searchParams,
}: Pick<AuctionDeleteDetailPageProps, "searchParams">) {
  return <AuctionDeleteDetailRoute searchParams={searchParams} />;
}

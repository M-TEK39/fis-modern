import FineDeleteDetailRoute, { type FineDeleteDetailPageProps } from "./_route";

export default function FineDeleteDetailPage({
  searchParams,
}: Pick<FineDeleteDetailPageProps, "searchParams">) {
  return <FineDeleteDetailRoute searchParams={searchParams} />;
}

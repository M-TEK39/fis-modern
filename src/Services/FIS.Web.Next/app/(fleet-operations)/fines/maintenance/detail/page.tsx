import FineDetailRoute, { type FineDetailPageProps } from "./_route";

export default function FineDetailPage({
  searchParams,
}: Pick<FineDetailPageProps, "searchParams">) {
  return <FineDetailRoute searchParams={searchParams} />;
}

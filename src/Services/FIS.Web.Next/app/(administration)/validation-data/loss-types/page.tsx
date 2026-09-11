import LossTypeListRoute, { type LossTypeListPageProps } from "./_route";

export default function LossTypeListPage({
  searchParams,
}: Pick<LossTypeListPageProps, "searchParams">) {
  return <LossTypeListRoute searchParams={searchParams} />;
}

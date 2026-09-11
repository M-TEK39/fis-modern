import ExtraCodeListRoute, { type ExtraCodeListPageProps } from "./_route";

export default function ExtraCodeListPage({
  searchParams,
}: Pick<ExtraCodeListPageProps, "searchParams">) {
  return <ExtraCodeListRoute searchParams={searchParams} />;
}

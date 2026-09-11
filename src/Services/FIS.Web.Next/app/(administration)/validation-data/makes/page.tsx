import MakeListRoute, { type MakeListPageProps } from "./_route";

export default function MakeListPage({ searchParams }: Pick<MakeListPageProps, "searchParams">) {
  return <MakeListRoute searchParams={searchParams} />;
}

import SiteListRoute, { type SiteListPageProps } from "./_route";

export default function SiteListPage({ searchParams }: Pick<SiteListPageProps, "searchParams">) {
  return <SiteListRoute searchParams={searchParams} />;
}

import {
  SiteListPageRoute,
  type SiteListPageProps,
} from "@/app/(administration)/validation-data/sites/_route";

export default function SitesPage({ searchParams }: Pick<SiteListPageProps, "searchParams">) {
  return <SiteListPageRoute searchParams={searchParams} routePath="/sites" />;
}

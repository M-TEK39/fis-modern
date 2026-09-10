import SiteListPage from "@/app/(administration)/validation-data/sites/page";

type SearchParams = Promise<Record<string, string | string[] | undefined>>;

export default function SitesPage({ searchParams }: Readonly<{ searchParams: SearchParams }>) {
  return <SiteListPage searchParams={searchParams} routePath="/sites" />;
}

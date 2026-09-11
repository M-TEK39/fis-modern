import {
  SiteListPageRoute,
  type SiteListPageProps,
} from "@/app/(administration)/validation-data/sites/_route";

export default function LegacySiteListPage(props: Pick<SiteListPageProps, "searchParams">) {
  return <SiteListPageRoute {...props} routePath="/Validation/MNT_Site.aspx" />;
}

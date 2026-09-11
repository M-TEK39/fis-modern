import {
  MakeListPageRoute,
  type MakeListPageProps,
} from "@/app/(administration)/validation-data/makes/_route";

export default function LegacyMakeListPage(props: Pick<MakeListPageProps, "searchParams">) {
  return <MakeListPageRoute {...props} routePath="/Validation/MNT_make.aspx" />;
}

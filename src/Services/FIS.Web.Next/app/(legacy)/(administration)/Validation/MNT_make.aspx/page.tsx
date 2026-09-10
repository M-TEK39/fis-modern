import MakeListPage from "@/app/(administration)/validation-data/makes/page";

export default function LegacyMakeListPage(props: Parameters<typeof MakeListPage>[0]) {
  return <MakeListPage {...props} routePath="/Validation/MNT_make.aspx" />;
}

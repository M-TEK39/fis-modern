import {
  ExtraCodeListPageRoute,
  type ExtraCodeListPageProps,
} from "@/app/(administration)/validation-data/extras/_route";

export default function LegacyExtraCodeListPage(
  props: Pick<ExtraCodeListPageProps, "searchParams">,
) {
  return <ExtraCodeListPageRoute {...props} routePath="/Validation/MNT_Extras.aspx" />;
}

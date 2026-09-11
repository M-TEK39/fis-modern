import {
  ModelListPageRoute,
  type ModelListPageProps,
} from "@/app/(administration)/validation-data/models/_route";

export default function LegacyModelListPage(props: Pick<ModelListPageProps, "searchParams">) {
  return <ModelListPageRoute {...props} routePath="/Validation/MNT_model.aspx" />;
}

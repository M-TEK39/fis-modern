import {
  ClassListPageRoute,
  type ClassListPageProps,
} from "@/app/(administration)/validation-data/classes/_route";

export default function LegacyClassListPage(props: Pick<ClassListPageProps, "searchParams">) {
  return <ClassListPageRoute {...props} routePath="/Validation/MNT_Class.aspx" />;
}

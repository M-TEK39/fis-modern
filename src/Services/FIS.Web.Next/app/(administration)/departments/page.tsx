import {
  DepartmentListPageRoute,
  type DepartmentListPageProps,
} from "@/app/(administration)/validation-data/departments/_route";

export default function DepartmentsPage(props: Pick<DepartmentListPageProps, "searchParams">) {
  return <DepartmentListPageRoute {...props} routePath="/departments" />;
}

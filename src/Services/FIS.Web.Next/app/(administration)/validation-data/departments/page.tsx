import DepartmentListRoute, { type DepartmentListPageProps } from "./_route";

export default function DepartmentListPage({
  searchParams,
}: Pick<DepartmentListPageProps, "searchParams">) {
  return <DepartmentListRoute searchParams={searchParams} />;
}

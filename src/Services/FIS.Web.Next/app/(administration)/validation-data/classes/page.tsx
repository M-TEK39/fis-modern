import ClassListRoute, { type ClassListPageProps } from "./_route";

export default function ClassListPage({ searchParams }: Pick<ClassListPageProps, "searchParams">) {
  return <ClassListRoute searchParams={searchParams} />;
}

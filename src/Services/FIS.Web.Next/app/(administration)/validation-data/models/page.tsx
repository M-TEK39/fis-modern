import ModelListRoute, { type ModelListPageProps } from "./_route";

export default function ModelListPage({ searchParams }: Pick<ModelListPageProps, "searchParams">) {
  return <ModelListRoute searchParams={searchParams} />;
}

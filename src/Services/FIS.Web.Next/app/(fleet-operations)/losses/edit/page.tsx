import EditLossRoute, { type EditLossPageProps } from "./_route";

export default function EditLossPage({ searchParams }: Pick<EditLossPageProps, "searchParams">) {
  return <EditLossRoute searchParams={searchParams} />;
}

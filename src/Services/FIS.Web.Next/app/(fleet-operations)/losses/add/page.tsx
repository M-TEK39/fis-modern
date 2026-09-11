import AddLossRoute, { type AddLossPageProps } from "./_route";

export default function AddLossPage({ searchParams }: Pick<AddLossPageProps, "searchParams">) {
  return <AddLossRoute searchParams={searchParams} />;
}

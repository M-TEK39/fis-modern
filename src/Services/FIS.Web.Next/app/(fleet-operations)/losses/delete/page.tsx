import DeleteLossRoute, { type DeleteLossPageProps } from "./_route";

export default function DeleteLossPage({
  searchParams,
}: Pick<DeleteLossPageProps, "searchParams">) {
  return <DeleteLossRoute searchParams={searchParams} />;
}

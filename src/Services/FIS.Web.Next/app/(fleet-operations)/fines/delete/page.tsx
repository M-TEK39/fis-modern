import FineDeleteRoute, { type FineDeletePageProps } from "./_route";

export default function FineDeletePage({
  searchParams,
}: Pick<FineDeletePageProps, "searchParams">) {
  return <FineDeleteRoute searchParams={searchParams} />;
}

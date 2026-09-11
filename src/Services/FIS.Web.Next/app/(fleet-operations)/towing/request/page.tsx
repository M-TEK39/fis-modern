import TowingRequestRoute, { type TowingRequestPageProps } from "./_route";

export default function TowingRequestPage({
  searchParams,
}: Pick<TowingRequestPageProps, "searchParams">) {
  return <TowingRequestRoute searchParams={searchParams} />;
}

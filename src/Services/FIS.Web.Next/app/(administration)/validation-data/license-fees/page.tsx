import LicenseFeeListRoute, { type LicenseFeeListPageProps } from "./_route";

export default function LicenseFeeListPage({
  searchParams,
}: Pick<LicenseFeeListPageProps, "searchParams">) {
  return <LicenseFeeListRoute searchParams={searchParams} />;
}

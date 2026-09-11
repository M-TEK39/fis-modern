import DriverLicenceListRoute, { type DriverLicenceListPageProps } from "./_route";

export default function DriverLicenceListPage({
  searchParams,
}: Pick<DriverLicenceListPageProps, "searchParams">) {
  return <DriverLicenceListRoute searchParams={searchParams} />;
}

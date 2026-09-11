import ReliefVehicleSearchRoute, { type ReliefVehicleSearchPageProps } from "./_route";

export default function ReliefVehicleSearchPage(
  props: Pick<ReliefVehicleSearchPageProps, "searchParams">,
) {
  return <ReliefVehicleSearchRoute {...props} />;
}

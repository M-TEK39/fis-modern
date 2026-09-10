import LossReportPage, {
  type LossReportPageProps,
} from "@/app/(fleet-operations)/losses/reports/[mode]/page";

export default function LegacyLossVehicleReport({
  searchParams,
}: Pick<LossReportPageProps, "searchParams">) {
  return (
    <LossReportPage
      params={Promise.resolve({ mode: "vehicle" })}
      searchParams={searchParams}
      routePath="/losses/RPT_loss_per_vehicle.htm"
    />
  );
}

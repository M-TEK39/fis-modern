import { CallCentreReportPage } from "@/app/call-centre/reports/[mode]/page";

export default function LegacyCallCentreOneVehicle(props: {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}) {
  return CallCentreReportPage({
    ...props,
    forcedMode: "one-vehicle",
    routePath: "/CallCentre/RPT_call_one_gg_main.aspx",
  });
}

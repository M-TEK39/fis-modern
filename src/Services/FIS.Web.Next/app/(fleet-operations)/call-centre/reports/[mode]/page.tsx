import CallCentreReportRoute, { type CallCentreReportPageProps } from "./_route";

export default function CallCentreReportPage(
  props: Pick<CallCentreReportPageProps, "searchParams">,
) {
  return <CallCentreReportRoute {...props} />;
}

import PrivateHireContractorReportPage from "@/app/(fleet-operations)/private-hire/reports/contractors/page";
import { StreamedRoute } from "@/components/app-shell/streamed-route";

type PrivateHireContractorReportPageProps = Parameters<typeof PrivateHireContractorReportPage>[0];

async function PrivateHireContractorReportPageContent(props: PrivateHireContractorReportPageProps) {
  return <PrivateHireContractorReportPage {...props} />;
}

export default function LegacyPrivateHireContractorReportPage(
  props: PrivateHireContractorReportPageProps,
) {
  return (
    <StreamedRoute>
      <PrivateHireContractorReportPageContent {...props} />
    </StreamedRoute>
  );
}

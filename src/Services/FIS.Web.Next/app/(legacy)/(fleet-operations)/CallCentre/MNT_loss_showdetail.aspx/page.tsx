import IncidentCapturePage, {
  type IncidentCapturePageProps,
} from "@/app/(fleet-operations)/call-centre/incident/capture/page";
import { StreamedRoute } from "@/components/app-shell/streamed-route";

function first(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

async function LegacyLossShowDetailPageContent({ searchParams }: IncidentCapturePageProps) {
  const values = await searchParams;
  const vmfCode = first(values.vmfCode) ?? first(values.ccVMF);
  const code = first(values.code) ?? first(values.cccode);

  return (
    <IncidentCapturePage
      searchParams={Promise.resolve({
        ...values,
        incidentType: "Loss_Theft",
        ...(vmfCode ? { vmfCode } : {}),
        ...(code ? { code } : {}),
        saved: "1",
      })}
    />
  );
}

export default function LegacyLossShowDetailPage(props: IncidentCapturePageProps) {
  return (
    <StreamedRoute>
      <LegacyLossShowDetailPageContent {...props} />
    </StreamedRoute>
  );
}

import IncidentCapturePage, {
  type IncidentCapturePageProps,
} from "@/app/(fleet-operations)/call-centre/incident/capture/page";
import { StreamedRoute } from "@/components/app-shell/streamed-route";

function first(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

async function LegacyLossCapturePageContent({ searchParams }: IncidentCapturePageProps) {
  const values = await searchParams;
  const vmfCode = first(values.vmfCode) ?? first(values.ccVMF);

  return (
    <IncidentCapturePage
      searchParams={Promise.resolve({
        ...values,
        incidentType: "Loss_Theft",
        lookupType: "GG",
        identifier: "",
        ...(vmfCode ? { vmfCode } : {}),
      })}
    />
  );
}

export default function LegacyLossCapturePage(props: IncidentCapturePageProps) {
  return (
    <StreamedRoute>
      <LegacyLossCapturePageContent {...props} />
    </StreamedRoute>
  );
}

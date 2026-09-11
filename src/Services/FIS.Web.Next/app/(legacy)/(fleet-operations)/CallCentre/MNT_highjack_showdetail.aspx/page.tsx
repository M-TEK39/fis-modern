import IncidentCapturePage, {
  type IncidentCapturePageProps,
} from "@/app/(fleet-operations)/call-centre/incident/capture/page";
import { StreamedRoute } from "@/components/app-shell/streamed-route";

function first(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

async function LegacyHiJackShowDetailPageContent({ searchParams }: IncidentCapturePageProps) {
  const values = await searchParams;
  const vmfCode = first(values.vmfCode) ?? first(values.ccVMF);
  const code = first(values.code) ?? first(values.cccode);

  return (
    <IncidentCapturePage
      searchParams={Promise.resolve({
        ...values,
        incidentType: "Hi-Jack",
        ...(vmfCode ? { vmfCode } : {}),
        ...(code ? { code } : {}),
        saved: "1",
      })}
    />
  );
}

export default function LegacyHiJackShowDetailPage(props: IncidentCapturePageProps) {
  return (
    <StreamedRoute>
      <LegacyHiJackShowDetailPageContent {...props} />
    </StreamedRoute>
  );
}

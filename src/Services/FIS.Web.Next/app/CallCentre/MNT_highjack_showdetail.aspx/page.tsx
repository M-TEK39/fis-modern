import IncidentCapturePage, {
  type IncidentCapturePageProps,
} from "@/app/call-centre/incident/capture/page";

function first(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

export default async function LegacyHiJackShowDetailPage({
  searchParams,
}: IncidentCapturePageProps) {
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

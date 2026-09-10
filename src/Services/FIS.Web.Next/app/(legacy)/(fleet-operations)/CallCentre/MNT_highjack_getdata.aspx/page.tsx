import IncidentCapturePage, {
  type IncidentCapturePageProps,
} from "@/app/(fleet-operations)/call-centre/incident/capture/page";

function first(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

export default async function LegacyHiJackCapturePage({ searchParams }: IncidentCapturePageProps) {
  const values = await searchParams;
  const vmfCode = first(values.vmfCode) ?? first(values.ccVMF);

  return (
    <IncidentCapturePage
      searchParams={Promise.resolve({
        ...values,
        incidentType: "Hi-Jack",
        lookupType: "GG",
        identifier: "",
        ...(vmfCode ? { vmfCode } : {}),
      })}
    />
  );
}

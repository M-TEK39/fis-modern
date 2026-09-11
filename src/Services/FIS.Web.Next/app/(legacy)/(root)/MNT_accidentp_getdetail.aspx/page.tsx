import { HqAddRoute } from "@/app/(fleet-operations)/accidents/hq/add/_route";
import HqEditPage from "@/app/(fleet-operations)/accidents/hq/edit/page";
import { StreamedRoute } from "@/components/app-shell/streamed-route";

type LegacyPtaDetailPageProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
};

function getQueryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

async function LegacyPtaDetailPageContent({ searchParams }: LegacyPtaDetailPageProps) {
  const query = await searchParams;
  const params = Promise.resolve(query);
  return getQueryValue(query.Action)?.toUpperCase() === "ADD" ? (
    <HqAddRoute searchParams={params} locationCode={2} />
  ) : (
    <HqEditPage searchParams={params} />
  );
}

export default function LegacyPtaDetailPage(props: LegacyPtaDetailPageProps) {
  return (
    <StreamedRoute>
      <LegacyPtaDetailPageContent {...props} />
    </StreamedRoute>
  );
}

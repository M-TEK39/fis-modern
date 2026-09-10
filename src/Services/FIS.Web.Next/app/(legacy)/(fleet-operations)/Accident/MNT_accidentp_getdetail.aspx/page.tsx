import HqAddPage from "@/app/(fleet-operations)/accidents/hq/add/page";
import HqEditPage from "@/app/(fleet-operations)/accidents/hq/edit/page";

type LegacyPtaDetailPageProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
};

function getQueryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

export default async function LegacyPtaDetailPage({ searchParams }: LegacyPtaDetailPageProps) {
  const query = await searchParams;
  const params = Promise.resolve(query);
  return getQueryValue(query.Action)?.toUpperCase() === "ADD"
    ? HqAddPage({ searchParams: params, locationCode: 2 })
    : HqEditPage({ searchParams: params });
}

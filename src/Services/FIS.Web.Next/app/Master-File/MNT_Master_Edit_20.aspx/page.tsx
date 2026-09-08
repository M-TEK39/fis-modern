import { redirect } from "next/navigation";

type LegacyEditFormPageProps = {
  searchParams: Promise<{ GGnum?: string | string[] }>;
};

function getQueryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

export default async function LegacyEditFormPage({ searchParams }: LegacyEditFormPageProps) {
  const query = await searchParams;
  const ggNumber = getQueryValue(query.GGnum)?.trim();
  const target = ggNumber
    ? `/vehicles/edit?searchTerm=${encodeURIComponent(ggNumber)}`
    : "/vehicles/edit";
  redirect(target);
}

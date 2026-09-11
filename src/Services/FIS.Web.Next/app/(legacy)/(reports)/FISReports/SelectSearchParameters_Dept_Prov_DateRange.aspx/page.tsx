import { redirect } from "next/navigation";
import { StreamedRoute } from "@/components/app-shell/streamed-route";

type Query = Record<string, string | string[] | undefined>;

function queryValue(query: Query, ...names: string[]) {
  for (const name of names) {
    const value = query[name];
    if (value !== undefined) return (Array.isArray(value) ? (value[0] ?? "") : value).trim();
  }
  return "";
}

async function LegacyMissingKilometresDateRangePageContent({
  searchParams,
}: Readonly<{ searchParams: Promise<Query> }>): Promise<never> {
  const query = await searchParams;
  const followPage = queryValue(query, "FollowPage", "followPage");
  const item =
    queryValue(query, "Item", "item") || followPage.match(/[?&]Item=([^&]+)/i)?.[1] || "";
  redirect(
    item.toLowerCase() === "comparebilledkilosandfuelconsumption"
      ? "/finance/missing-kilometres/fuel-consumption"
      : "/finance",
  );
}

export default function LegacyMissingKilometresDateRangePage(
  props: Readonly<{ searchParams: Promise<Query> }>,
) {
  return (
    <StreamedRoute>
      <LegacyMissingKilometresDateRangePageContent {...props} />
    </StreamedRoute>
  );
}

import { redirect } from "next/navigation";
import { StreamedRoute } from "@/components/app-shell/streamed-route";

type Query = Record<string, string | string[] | undefined>;

function queryValue(query: Query, ...names: string[]) {
  for (const name of names) {
    const value = query[name];
    if (value !== undefined) return Array.isArray(value) ? (value[0] ?? "") : value;
  }
  return "";
}

async function LegacyFinanceSearchParametersPageContent({
  searchParams,
}: Readonly<{ searchParams: Promise<Query> }>): Promise<never> {
  const query = await searchParams;
  const followPage = queryValue(query, "FollowPage", "followPage");
  const item = (
    queryValue(query, "Item", "item") ||
    followPage.match(/[?&]Item=([^&]+)/i)?.[1] ||
    ""
  ).toLowerCase();
  if (item === "vehicleswithnokilosconsumingfuel")
    redirect("/finance/missing-kilometres/no-kilos-consuming-fuel");
  const action =
    item === "invoicedamountspermonth"
      ? "income-department"
      : item === "invoicedamountspermonthpersite"
        ? "income-department-site"
        : item === "invoicedamountspermonthpersitepervehicle"
          ? "download-income-department-site-vehicle"
          : "income-department";
  redirect(`/finance/reports/${action}`);
}

export default function LegacyFinanceSearchParametersPage(
  props: Readonly<{ searchParams: Promise<Query> }>,
) {
  return (
    <StreamedRoute>
      <LegacyFinanceSearchParametersPageContent {...props} />
    </StreamedRoute>
  );
}

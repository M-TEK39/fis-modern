import { redirect } from "next/navigation";

import { StreamedRoute } from "@/components/app-shell/streamed-route";

import { queryValue, type LegacyFinanceQuery } from "../legacy-finance-redirect";

async function LegacyTripKilometresContent({
  searchParams,
}: Readonly<{ searchParams: Promise<LegacyFinanceQuery> }>): Promise<never> {
  const query = await searchParams;
  const params = new URLSearchParams();
  const report = queryValue(query, "Report", "report", "Item", "item");
  const departmentCode = queryValue(query, "DepID", "departmentCode");
  const siteCode = queryValue(query, "SiteID", "siteCode");
  const startDate = queryValue(query, "StartDate", "startDate");
  const endDate = queryValue(query, "EndDate", "endDate");
  if (report) params.set("report", report);
  if (departmentCode) params.set("departmentCode", departmentCode);
  if (siteCode) params.set("siteCode", siteCode);
  if (startDate) params.set("startDate", startDate);
  if (endDate) params.set("endDate", endDate);
  redirect(`/finance/trip-kilometres?${params.toString()}`);
}

export default function LegacyTripKilometresPage(
  props: Readonly<{ searchParams: Promise<LegacyFinanceQuery> }>,
) {
  return (
    <StreamedRoute>
      <LegacyTripKilometresContent {...props} />
    </StreamedRoute>
  );
}

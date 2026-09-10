import { redirect } from "next/navigation";

import AuctionReportsPage from "@/app/(fleet-operations)/auction/reports/page";

type LegacyAuctionReportsPageProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
};

const LEGACY_REPORT_MODES = {
  "one-vehicle": "one-vehicle",
  "all-vehicles": "all-vehicles",
  "sale-to-name": "sale-to-name",
  "one-auction-sort-gg": "auction-gg",
  "one-auction-sort-lot": "auction-lot",
} as const;

function first(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

function copyQueryValue(
  source: Record<string, string | string[] | undefined>,
  target: URLSearchParams,
  targetName: string,
  ...sourceNames: string[]
) {
  for (const sourceName of sourceNames) {
    const value = first(source[sourceName]);
    if (value) {
      target.set(targetName, value);
      return;
    }
  }
}

export default async function ReportsAuctionPage({ searchParams }: LegacyAuctionReportsPageProps) {
  const query = await searchParams;
  const reportType = first(query.rtype)?.trim().toLowerCase();
  const mode = reportType
    ? LEGACY_REPORT_MODES[reportType as keyof typeof LEGACY_REPORT_MODES]
    : undefined;

  if (first(query.view)?.toLowerCase() === "report" && mode) {
    const target = new URLSearchParams({ run: "1" });
    copyQueryValue(query, target, "vmfCode", "vmfCode");
    copyQueryValue(query, target, "searchQuery", "searchQuery", "xnumber");
    copyQueryValue(query, target, "searchType", "searchType", "Radio1");
    copyQueryValue(query, target, "garage", "garage");
    copyQueryValue(query, target, "auctionNumber", "auctionNumber", "xaucnumber");
    copyQueryValue(query, target, "buyerName", "buyerName", "xbname");
    redirect(`/auction/reports/${mode}?${target.toString()}`);
  }

  return <AuctionReportsPage routePath="/reports/auction" />;
}

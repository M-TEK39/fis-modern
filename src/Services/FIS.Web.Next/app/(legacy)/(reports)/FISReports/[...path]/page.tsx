import { redirect } from "next/navigation";

import ReportsPage from "@/app/(fleet-operations)/reports/page";
import { ReportsRoutePage } from "@/app/(fleet-operations)/reports/[slug]/_route";
import { StreamedRoute } from "@/components/app-shell/streamed-route";

import {
  legacyPageForRoute,
  legacyRedirectPath,
  type LegacyReportQuery,
} from "./legacy-report-routing";

type Props = Readonly<{
  params: Promise<{ path: string[] }>;
  searchParams: Promise<LegacyReportQuery>;
}>;

async function LegacyFisReportsPageContent({ params, searchParams }: Props) {
  const { path } = await params;
  const route = path.join("/").toLowerCase();
  const query = await searchParams;

  const legacyPage = legacyPageForRoute(route);
  if (legacyPage?.kind === "reports") return <ReportsPage />;
  if (legacyPage?.kind === "route")
    return <ReportsRoutePage slug={legacyPage.slug} searchParams={Promise.resolve(query)} />;

  redirect(legacyRedirectPath(route, query));
}

export default function LegacyFisReportsPage(props: Props) {
  return (
    <StreamedRoute>
      <LegacyFisReportsPageContent {...props} />
    </StreamedRoute>
  );
}

import { createElement, Suspense } from "react";

import RouteLoading from "@/components/app-shell/route-loading";

import { NoticeManagementContent } from "./_route";

type SearchParams = Promise<Record<string, string | string[] | undefined>>;
type NoticeManagementRouteProps = Readonly<{ searchParams: SearchParams }>;

export function createLegacyNoticeManagementPage(
  routePath: "/notice-management" | "/Admin/NoticeManagement.aspx",
) {
  return function LegacyNoticeManagementPage({ searchParams }: NoticeManagementRouteProps) {
    return createElement(
      Suspense,
      { fallback: createElement(RouteLoading) },
      createElement(NoticeManagementContent, { routePath, searchParams }),
    );
  };
}

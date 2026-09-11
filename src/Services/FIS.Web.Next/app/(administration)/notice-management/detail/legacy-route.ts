import { createElement, Suspense } from "react";

import RouteLoading from "@/components/app-shell/route-loading";

import { NoticeDetailContent } from "./_route";

type SearchParams = Promise<Record<string, string | string[] | undefined>>;
type NoticeDetailRouteProps = Readonly<{ searchParams: SearchParams }>;

export function createLegacyNoticeDetailPage(
  routePath: "/notice-management/detail" | "/Admin/NoticeDetailManagement.aspx",
) {
  return function LegacyNoticeDetailPage({ searchParams }: NoticeDetailRouteProps) {
    return createElement(
      Suspense,
      { fallback: createElement(RouteLoading) },
      createElement(NoticeDetailContent, { routePath, searchParams }),
    );
  };
}

import { Suspense } from "react";

import RouteLoading from "@/components/app-shell/route-loading";
import {
  UserAdminListPage,
  type UserAdminSearchParams,
} from "@/app/(administration)/users/user-admin-content";

export default function UserAdminViewPage({
  searchParams,
}: Readonly<{ searchParams: UserAdminSearchParams }>) {
  return (
    <Suspense fallback={<RouteLoading />}>
      <UserAdminListPage searchParams={searchParams} routePath="/users/view" />
    </Suspense>
  );
}

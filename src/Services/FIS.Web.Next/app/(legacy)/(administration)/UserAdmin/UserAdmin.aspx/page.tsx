import { Suspense } from "react";

import RouteLoading from "@/components/app-shell/route-loading";
import {
  UserAdminListPage,
  type UserAdminSearchParams,
} from "@/app/(administration)/users/user-admin-content";

export default function LegacyUserAdminListPage({
  searchParams,
}: Readonly<{ searchParams: UserAdminSearchParams }>) {
  return (
    <Suspense fallback={<RouteLoading />}>
      <UserAdminListPage searchParams={searchParams} />
    </Suspense>
  );
}

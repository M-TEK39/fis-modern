import { Suspense } from "react";

import RouteLoading from "@/components/app-shell/route-loading";
import {
  UserAdminMenuPage,
  type UserAdminSearchParams,
} from "@/app/(administration)/users/user-admin-content";

export default function UserAdminPage({
  searchParams,
}: Readonly<{ searchParams: UserAdminSearchParams }>) {
  return (
    <Suspense fallback={<RouteLoading />}>
      <UserAdminMenuPage searchParams={searchParams} />
    </Suspense>
  );
}

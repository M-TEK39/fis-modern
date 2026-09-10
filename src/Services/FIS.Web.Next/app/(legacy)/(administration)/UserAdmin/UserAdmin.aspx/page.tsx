import {
  UserAdminListPage,
  type UserAdminSearchParams,
} from "@/app/(administration)/users/user-admin-content";

export default function LegacyUserAdminListPage({
  searchParams,
}: Readonly<{ searchParams: UserAdminSearchParams }>) {
  return <UserAdminListPage searchParams={searchParams} />;
}

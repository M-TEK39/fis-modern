import { UserAdminListPage, type UserAdminSearchParams } from "@/app/users/user-admin-content";

export default function UserAdminViewPage({
  searchParams,
}: Readonly<{ searchParams: UserAdminSearchParams }>) {
  return <UserAdminListPage searchParams={searchParams} routePath="/users/view" />;
}

import { UserAdminMenuPage, type UserAdminSearchParams } from "@/app/users/user-admin-content";

export default function UserAdminPage({
  searchParams,
}: Readonly<{ searchParams: UserAdminSearchParams }>) {
  return <UserAdminMenuPage searchParams={searchParams} />;
}

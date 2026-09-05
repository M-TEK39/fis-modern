import { UserAdminMenuPage, type UserAdminSearchParams } from "@/app/users/user-admin-content";

export default function LegacyUserAdminMenuPage({
  searchParams,
}: Readonly<{ searchParams: UserAdminSearchParams }>) {
  return <UserAdminMenuPage searchParams={searchParams} />;
}

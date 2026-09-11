import NoticeManagementRoute from "./_route";

type SearchParams = Promise<Record<string, string | string[] | undefined>>;

export default function NoticeManagementPage({
  searchParams,
}: Readonly<{ searchParams: SearchParams }>) {
  return <NoticeManagementRoute searchParams={searchParams} />;
}

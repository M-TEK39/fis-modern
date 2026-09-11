import NoticeDetailRoute from "./_route";

type SearchParams = Promise<Record<string, string | string[] | undefined>>;

export default function NoticeDetailPage({
  searchParams,
}: Readonly<{ searchParams: SearchParams }>) {
  return <NoticeDetailRoute searchParams={searchParams} />;
}

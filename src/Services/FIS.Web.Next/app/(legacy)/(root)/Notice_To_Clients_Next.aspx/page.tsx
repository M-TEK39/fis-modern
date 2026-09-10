import NoticeToClientsPage from "@/app/(platform)/notice-to-clients/page";

export default function LegacyNextNoticeToClientsPage(
  props: Parameters<typeof NoticeToClientsPage>[0],
) {
  return <NoticeToClientsPage {...props} />;
}

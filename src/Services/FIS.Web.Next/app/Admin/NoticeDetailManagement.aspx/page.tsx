import NoticeDetailPage from "@/app/notice-management/detail/page";

export default function LegacyNoticeDetailManagementPage(
  props: Parameters<typeof NoticeDetailPage>[0],
) {
  return <NoticeDetailPage {...props} routePath="/Admin/NoticeDetailManagement.aspx" />;
}

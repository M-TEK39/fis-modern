import NoticeManagementPage from "@/app/notice-management/page";

export default function LegacyNoticeManagementPage(props: Parameters<typeof NoticeManagementPage>[0]) {
  return <NoticeManagementPage {...props} routePath="/Admin/NoticeManagement.aspx" />;
}

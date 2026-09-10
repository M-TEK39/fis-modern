import TrafficDeptDetailPage from "@/app/(fleet-operations)/fines/traffic-dept/detail/page";
import type { TrafficDeptDetailPageProps } from "@/app/(fleet-operations)/fines/traffic-dept/detail/page";

export default function LegacyTrafficDeptDetailPage(props: TrafficDeptDetailPageProps) {
  return <TrafficDeptDetailPage {...props} routePath="/fines/MNT_Traffic_getdetail.aspx" />;
}

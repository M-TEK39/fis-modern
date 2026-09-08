import TrafficDeptPage from "@/app/fines/traffic-dept/page";
import type { TrafficDeptPageProps } from "@/app/fines/traffic-dept/page";

export default function LegacyTrafficDeptPage(props: TrafficDeptPageProps) {
  return <TrafficDeptPage {...props} routePath="/fines/MNT_Traffic_getdata.aspx" />;
}

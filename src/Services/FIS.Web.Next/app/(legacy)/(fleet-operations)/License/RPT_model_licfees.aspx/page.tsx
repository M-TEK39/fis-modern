import { LicenseReportPage } from "@/app/(fleet-operations)/licenses/reports/[mode]/page";

export default function LegacyReport(props: Parameters<typeof LicenseReportPage>[0]) {
  return <LicenseReportPage {...props} forcedMode="model-fees" />;
}

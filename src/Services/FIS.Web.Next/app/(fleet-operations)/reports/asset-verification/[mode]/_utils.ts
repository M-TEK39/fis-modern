import { notFound } from "next/navigation";

export const REPORT_MODES = [
  "per-site-province-date",
  "not-verified",
  "verified-by-date-range",
] as const;

export type AssetVerificationReportMode = (typeof REPORT_MODES)[number];

export function isAssetVerificationReportMode(value: string): value is AssetVerificationReportMode {
  return REPORT_MODES.includes(value as AssetVerificationReportMode);
}

export function assetVerificationReportMode(value: string): AssetVerificationReportMode {
  if (!isAssetVerificationReportMode(value)) notFound();
  return value;
}

import { notFound } from "next/navigation";

export const REPORT_MODES = [
  "one-vehicle",
  "print-job-card",
  "period",
  "in-workshop",
  "merchants",
] as const;

export type WorkshopReportMode = (typeof REPORT_MODES)[number];

export function workshopReportMode(value: string): WorkshopReportMode {
  if (!REPORT_MODES.includes(value as WorkshopReportMode)) notFound();
  return value as WorkshopReportMode;
}

import "server-only";

import { headers } from "next/headers";
import { redirect } from "next/navigation";

import { FinanceApiError, getBatchStatus } from "@/lib/api/finance/api-finance";
import { isBatchProgressExemptPath } from "@/lib/finance/batch-progress-paths";

export async function redirectIfBatchBlocksRoute() {
  const pathname = (await headers()).get("x-fis-pathname") ?? "";
  if (isBatchProgressExemptPath(pathname)) return;

  try {
    const batch = await getBatchStatus();
    if (batch.isActive) redirect("/batch-in-progress");
  } catch (error) {
    if (error instanceof FinanceApiError) return;
    throw error;
  }
}

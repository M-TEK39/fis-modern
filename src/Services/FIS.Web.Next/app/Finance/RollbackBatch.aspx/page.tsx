import { redirect } from "next/navigation";

export default function LegacyRollbackBatchPage() {
  redirect("/finance/batch-management/rollback");
}

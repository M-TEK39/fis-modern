import { redirect } from "next/navigation";

export default function LegacyStartBatchPage() {
  redirect("/finance/batch-management/start");
}

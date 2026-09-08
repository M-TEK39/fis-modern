import { redirect } from "next/navigation";

export default function LegacyBasViewPage() {
  redirect("/finance/financial-allocation/view-bas");
}

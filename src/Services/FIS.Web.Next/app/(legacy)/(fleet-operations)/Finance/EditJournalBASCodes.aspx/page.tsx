import { redirect } from "next/navigation";

export default function LegacyBasFixPage() {
  redirect("/finance/financial-allocation/fix-invalid-journals");
}

import { redirect } from "next/navigation";

export default function LegacyBasFundPage() {
  redirect("/finance/financial-allocation/allocate-fund-codes");
}

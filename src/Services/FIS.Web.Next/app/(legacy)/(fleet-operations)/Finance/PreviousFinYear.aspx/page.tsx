import { redirect } from "next/navigation";

import { StreamedRoute } from "@/components/app-shell/streamed-route";

async function LegacyPreviousFinancialYearContent(): Promise<never> {
  redirect("/reports/previous-fin-year");
}

export default function LegacyPreviousFinancialYearPage() {
  return (
    <StreamedRoute>
      <LegacyPreviousFinancialYearContent />
    </StreamedRoute>
  );
}

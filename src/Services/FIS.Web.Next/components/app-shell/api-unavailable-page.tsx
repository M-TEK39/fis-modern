import type { ReactNode } from "react";

import ApiUnavailableCard from "@/components/app-shell/api-unavailable-card";

type ApiUnavailablePageProps = Readonly<{
  message: ReactNode;
  retryHref: string;
}>;

export default function ApiUnavailablePage({ message, retryHref }: ApiUnavailablePageProps) {
  return (
    <main className="page-shell vehicle-page-shell">
      <ApiUnavailableCard
        message={message}
        retryHref={retryHref}
        description="Retry when the FIS API is available."
        showIcon={false}
        headingLevel="h1"
      />
    </main>
  );
}

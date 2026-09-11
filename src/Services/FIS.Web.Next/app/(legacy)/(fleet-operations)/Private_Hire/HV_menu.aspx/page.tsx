import PrivateHirePage from "@/app/(fleet-operations)/private-hire/page";
import { StreamedRoute } from "@/components/app-shell/streamed-route";

export default function LegacyPrivateHireMenu({
  searchParams,
}: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  return (
    <StreamedRoute>
      <PrivateHirePage searchParams={searchParams} />
    </StreamedRoute>
  );
}

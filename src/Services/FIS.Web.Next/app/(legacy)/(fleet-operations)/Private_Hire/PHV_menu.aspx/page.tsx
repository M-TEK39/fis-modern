import PrivateHireMaintenanceMenuPage from "@/app/(fleet-operations)/private-hire/maintenance-menu/page";
import { StreamedRoute } from "@/components/app-shell/streamed-route";

async function PrivateHireMaintenanceMenuPageContent() {
  return <PrivateHireMaintenanceMenuPage />;
}

export default function LegacyPrivateHireMaintenanceMenuPage() {
  return (
    <StreamedRoute>
      <PrivateHireMaintenanceMenuPageContent />
    </StreamedRoute>
  );
}

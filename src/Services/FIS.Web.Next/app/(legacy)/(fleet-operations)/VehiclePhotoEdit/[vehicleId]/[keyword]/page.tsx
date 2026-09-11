import { redirect } from "next/navigation";
import { StreamedRoute } from "@/components/app-shell/streamed-route";

type PageProps = { params: Promise<{ vehicleId: string; keyword: string }> };

async function LegacyVehiclePhotoEditPageContent({ params }: PageProps): Promise<never> {
  const { vehicleId, keyword } = await params;
  if (!/^\d+$/.test(vehicleId)) redirect("/vehicle-photos");
  redirect(`/vehicle-photos/manage/${vehicleId}?keyword=${encodeURIComponent(keyword)}`);
}

export default function LegacyVehiclePhotoEditPage(props: PageProps) {
  return (
    <StreamedRoute>
      <LegacyVehiclePhotoEditPageContent {...props} />
    </StreamedRoute>
  );
}

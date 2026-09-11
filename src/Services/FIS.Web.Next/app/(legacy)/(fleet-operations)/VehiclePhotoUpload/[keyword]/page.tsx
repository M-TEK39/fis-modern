import { redirect } from "next/navigation";
import { StreamedRoute } from "@/components/app-shell/streamed-route";

type PageProps = { params: Promise<{ keyword: string }> };

async function LegacyVehiclePhotoUploadPageContent({ params }: PageProps): Promise<never> {
  const { keyword } = await params;
  redirect(`/vehicle-photos?q=${encodeURIComponent(keyword)}`);
}

export default function LegacyVehiclePhotoUploadPage(props: PageProps) {
  return (
    <StreamedRoute>
      <LegacyVehiclePhotoUploadPageContent {...props} />
    </StreamedRoute>
  );
}

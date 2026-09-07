import { redirect } from "next/navigation";

type PageProps = { params: Promise<{ keyword: string }> };

export default async function LegacyVehiclePhotoUploadPage({ params }: PageProps) {
  const { keyword } = await params;
  redirect(`/vehicle-photos?q=${encodeURIComponent(keyword)}`);
}

import { redirect } from "next/navigation";

type PageProps = { params: Promise<{ vehicleId: string; keyword: string }> };

export default async function LegacyVehiclePhotoEditPage({ params }: PageProps) {
  const { vehicleId, keyword } = await params;
  if (!/^\d+$/.test(vehicleId)) redirect("/vehicle-photos");
  redirect(`/vehicle-photos/manage/${vehicleId}?keyword=${encodeURIComponent(keyword)}`);
}

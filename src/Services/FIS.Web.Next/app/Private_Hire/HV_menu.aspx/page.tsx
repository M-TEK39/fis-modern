import PrivateHirePage from "@/app/private-hire/page";

export default function LegacyPrivateHireMenu({
  searchParams,
}: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  return <PrivateHirePage searchParams={searchParams} />;
}

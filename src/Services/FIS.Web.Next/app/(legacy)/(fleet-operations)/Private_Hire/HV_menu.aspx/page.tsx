import PrivateHirePage from "@/app/(fleet-operations)/private-hire/page";

export default function LegacyPrivateHireMenu({
  searchParams,
}: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  return <PrivateHirePage searchParams={searchParams} />;
}

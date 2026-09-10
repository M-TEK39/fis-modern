import ClearanceEntryPage from "@/app/(fleet-operations)/clearance/entry/page";

type LegacyClearanceEntryProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
};

export default function LegacyClearanceEntryPage({ searchParams }: LegacyClearanceEntryProps) {
  return (
    <ClearanceEntryPage routePath="/clearance/MNT_Clear_Getreg.aspx" searchParams={searchParams} />
  );
}

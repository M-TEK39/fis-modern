import ClearanceEntryPage from "@/app/(fleet-operations)/clearance/entry/page";

type LegacyClearanceAddProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
};

export default function LegacyClearanceAddPage({ searchParams }: LegacyClearanceAddProps) {
  return (
    <ClearanceEntryPage routePath="/Clearance/MNT_Clearance_add.aspx" searchParams={searchParams} />
  );
}

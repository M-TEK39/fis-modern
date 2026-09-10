import ClearanceEntryPage from "@/app/(fleet-operations)/clearance/entry/page";

type LegacyClearanceEditProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
};

export default function LegacyClearanceEditPage({ searchParams }: LegacyClearanceEditProps) {
  return (
    <ClearanceEntryPage
      forcedAction="edit"
      routePath="/Clearance/MNT_Clearance_Edit.aspx"
      searchParams={searchParams}
    />
  );
}

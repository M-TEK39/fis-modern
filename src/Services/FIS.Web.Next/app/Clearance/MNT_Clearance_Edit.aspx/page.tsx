import ClearanceEntryPage from "@/app/clearance/entry/page";

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

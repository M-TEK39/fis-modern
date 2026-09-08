import ClearanceEntryPage from "@/app/clearance/entry/page";

type LegacyClearanceDeleteProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
};

export default function LegacyClearanceDeletePage({ searchParams }: LegacyClearanceDeleteProps) {
  return (
    <ClearanceEntryPage
      forcedAction="delete"
      routePath="/Clearance/MNT_Clearance_Del.aspx"
      searchParams={searchParams}
    />
  );
}

import GgBlockNumbersPage from "@/app/(fleet-operations)/vehicles/gg-block-numbers/page";

export default function LegacyGgBlockNumbersPage() {
  return (
    <GgBlockNumbersPage
      searchParams={Promise.resolve({})}
      routePath="/Master-File/Add_GGBlockNumbers.aspx"
    />
  );
}

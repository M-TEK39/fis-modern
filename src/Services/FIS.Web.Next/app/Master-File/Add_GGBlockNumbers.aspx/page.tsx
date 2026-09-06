import GgBlockNumbersPage from "@/app/vehicles/gg-block-numbers/page";

export default function LegacyGgBlockNumbersPage() {
  return <GgBlockNumbersPage searchParams={Promise.resolve({})} routePath="/Master-File/Add_GGBlockNumbers.aspx" />;
}

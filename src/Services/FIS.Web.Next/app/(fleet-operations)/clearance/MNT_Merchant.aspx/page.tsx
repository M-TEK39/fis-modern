import ClearanceMerchantPage from "@/app/(fleet-operations)/clearance/merchant/page";

type LegacyMerchantMenuProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
};

export default function LegacyMerchantMenuPage({ searchParams }: LegacyMerchantMenuProps) {
  return (
    <ClearanceMerchantPage routePath="/clearance/MNT_Merchant.aspx" searchParams={searchParams} />
  );
}

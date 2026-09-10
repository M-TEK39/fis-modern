import ClearanceMerchantPage from "@/app/(fleet-operations)/clearance/merchant/page";

type LegacyMerchantEditProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
};

export default function LegacyMerchantEditPage({ searchParams }: LegacyMerchantEditProps) {
  return (
    <ClearanceMerchantPage
      routePath="/clearance/MNT_Merchant_Edit.aspx"
      searchParams={searchParams}
    />
  );
}

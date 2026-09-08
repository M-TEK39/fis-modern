import ClearanceMerchantPage from "@/app/clearance/merchant/page";

type LegacyMerchantAddProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
};

export default function LegacyMerchantAddPage({ searchParams }: LegacyMerchantAddProps) {
  return (
    <ClearanceMerchantPage
      routePath="/clearance/MNT_Merchant_Add.aspx"
      searchParams={searchParams}
    />
  );
}

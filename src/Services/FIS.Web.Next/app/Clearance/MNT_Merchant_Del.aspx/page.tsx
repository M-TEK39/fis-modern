import ClearanceMerchantPage from "@/app/clearance/merchant/page";

type LegacyMerchantDeleteProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
};

export default function LegacyMerchantDeletePage({ searchParams }: LegacyMerchantDeleteProps) {
  return (
    <ClearanceMerchantPage
      deletionMode
      routePath="/Clearance/MNT_Merchant_Del.aspx"
      searchParams={searchParams}
    />
  );
}

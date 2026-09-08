import ClearanceMerchantPage from "@/app/clearance/merchant/page";

type LegacyMerchantDeleteCheckProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
};

export default function LegacyMerchantDeleteCheckPage({
  searchParams,
}: LegacyMerchantDeleteCheckProps) {
  return (
    <ClearanceMerchantPage
      deletionMode
      routePath="/Clearance/MNT_Merchant_Del_Check.aspx"
      searchParams={searchParams}
    />
  );
}

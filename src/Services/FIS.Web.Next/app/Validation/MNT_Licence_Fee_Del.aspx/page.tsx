import { redirect } from "next/navigation";

type LicenseFeeDeletePageProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
};

function getQueryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

export default async function LicenseFeeDeletePage({ searchParams }: LicenseFeeDeletePageProps) {
  const query = await searchParams;
  const code = getQueryValue(query.code) ?? getQueryValue(query.licenceFeeCode) ?? "";
  redirect(`/Validation/MNT_Licence_Fee_Del_Check.aspx?code=${encodeURIComponent(code)}`);
}

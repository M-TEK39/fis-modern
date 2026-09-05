import { redirect } from "next/navigation";

type SiteDeletePageProps = { searchParams: Promise<Record<string, string | string[] | undefined>> };
function queryValue(value: string | string[] | undefined) { return Array.isArray(value) ? value[0] : value; }

export default async function SiteDeletePage({ searchParams }: SiteDeletePageProps) {
  const query = await searchParams;
  const code = queryValue(query.code) ?? queryValue(query.siteCode) ?? "";
  redirect(`/Validation/MNT_Site_Del_Check.aspx?code=${encodeURIComponent(code)}`);
}

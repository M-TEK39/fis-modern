import { redirect } from "next/navigation";

type MakeDeletePageProps = { searchParams: Promise<Record<string, string | string[] | undefined>> };
function getQueryValue(value: string | string[] | undefined) { return Array.isArray(value) ? value[0] : value; }
export default async function MakeDeletePage({ searchParams }: MakeDeletePageProps) { const query = await searchParams; const code = getQueryValue(query.code) ?? getQueryValue(query.makeCode) ?? ""; redirect(`/Validation/MNT_Make_Del_Check.aspx?code=${encodeURIComponent(code)}`); }

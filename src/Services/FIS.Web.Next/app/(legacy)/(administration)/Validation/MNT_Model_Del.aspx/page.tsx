import { redirect } from "next/navigation";

type ModelDeletePageProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
};

function getQueryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

export default async function ModelDeletePage({ searchParams }: ModelDeletePageProps) {
  const query = await searchParams;
  const code = getQueryValue(query.code) ?? getQueryValue(query.modelCode) ?? "";
  redirect(`/Validation/MNT_Model_Del_Check.aspx?code=${encodeURIComponent(code)}`);
}

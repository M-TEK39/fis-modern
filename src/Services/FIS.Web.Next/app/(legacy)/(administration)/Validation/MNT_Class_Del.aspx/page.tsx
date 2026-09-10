import { redirect } from "next/navigation";

type ClassDeletePageProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
};

function getQueryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

export default async function ClassDeletePage({ searchParams }: ClassDeletePageProps) {
  const query = await searchParams;
  const code = getQueryValue(query.code) ?? getQueryValue(query.classCode) ?? "";
  redirect(`/Validation/MNT_Class_Del_Check.aspx?code=${encodeURIComponent(code)}`);
}

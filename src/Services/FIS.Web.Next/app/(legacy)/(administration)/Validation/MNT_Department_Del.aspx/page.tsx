import { redirect } from "next/navigation";

type DepartmentDeletePageProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
};

function getQueryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

export default async function DepartmentDeletePage({ searchParams }: DepartmentDeletePageProps) {
  const query = await searchParams;
  const code = getQueryValue(query.code) ?? getQueryValue(query.cmbdep) ?? "";
  redirect(`/Validation/MNT_Department_Del_Check.aspx?code=${encodeURIComponent(code)}`);
}

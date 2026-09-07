import { redirect } from "next/navigation";

type SearchParams = Promise<Record<string, string | string[] | undefined>>;

function getQueryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

export default async function LegacyAngularEntryPage({ searchParams }: Readonly<{ searchParams: SearchParams }>) {
  const query = await searchParams;
  const sub = (getQueryValue(query.sub) ?? "SiteStaff").trim().toLowerCase();
  const departmentCode = getQueryValue(query.departmentCode);
  const siteCode = getQueryValue(query.siteCode);
  const keyword = getQueryValue(query.keyword) ?? getQueryValue(query.q);
  const context = new URLSearchParams();
  if (departmentCode) context.set("departmentCode", departmentCode);
  if (siteCode) context.set("siteCode", siteCode);
  if (keyword) context.set("q", keyword);
  const suffix = context.size > 0 ? `?${context.toString()}` : "";

  if (sub === "vehiclephotoupload") redirect(`/vehicle-photos${suffix}`);
  if (sub === "vehiclephotoedit") {
    const vehicleId = getQueryValue(query.vehicleId);
    if (vehicleId && /^\d+$/.test(vehicleId)) redirect(`/vehicle-photos/manage/${vehicleId}${suffix}`);
  }
  if (sub === "authorisermanagement") redirect(`/drivers/authorisers${suffix}`);
  if (sub === "authoriseredit") redirect(`/drivers/authorisers/edit${suffix}`);
  if (sub === "sitedrivermanagement") redirect(`/drivers/site-drivers${suffix}`);
  if (sub === "sitedriveredit") redirect(`/drivers/site-drivers/edit${suffix}`);
  redirect(`/drivers${suffix}`);
}

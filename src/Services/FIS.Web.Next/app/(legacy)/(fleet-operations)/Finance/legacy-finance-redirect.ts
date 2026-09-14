export type LegacyFinanceQuery = Record<string, string | string[] | undefined>;

export function queryValue(query: LegacyFinanceQuery, ...names: string[]) {
  for (const name of names) {
    const value = query[name];
    if (value !== undefined) return (Array.isArray(value) ? (value[0] ?? "") : value).trim();
  }
  return "";
}

export function pathWithLegacyQuery(path: string, query: LegacyFinanceQuery) {
  const params = new URLSearchParams();
  for (const [name, rawValue] of Object.entries(query)) {
    if (Array.isArray(rawValue)) {
      for (const value of rawValue) params.append(name, value);
    } else if (rawValue !== undefined) {
      params.append(name, rawValue);
    }
  }
  const serialized = params.toString();
  return serialized ? `${path}?${serialized}` : path;
}

export function auditTrailPath(query: LegacyFinanceQuery) {
  const mode = queryValue(query, "Mode", "mode").toLowerCase();
  return `/finance/audit-trail/${mode === "site" || mode === "vehicle" ? mode : "department"}`;
}

export function batchManagementPath(query: LegacyFinanceQuery) {
  return queryValue(query, "status").toLowerCase() === "end"
    ? "/finance/batch-management/finish"
    : "/finance/batch-management/start";
}

function numberedParameter(query: LegacyFinanceQuery, ...names: string[]) {
  const expected = new Set(names.map((name) => name.toLowerCase()));
  for (let index = 1; index <= 20; index += 1) {
    const name = queryValue(query, `ParamName${index}`).toLowerCase();
    if (expected.has(name)) return queryValue(query, `ParamValue${index}`);
  }
  return "";
}

function isoDate(value: string) {
  const trimmed = value.trim();
  if (/^\d{4}-\d{2}-\d{2}$/.test(trimmed)) return trimmed;
  const legacyParts = trimmed.match(/^(\d{1,2})\/(\d{1,2})\/(\d{4})$/);
  if (!legacyParts) return "";
  const [, day, month, year] = legacyParts;
  const parsed = new Date(Date.UTC(Number(year), Number(month) - 1, Number(day)));
  return parsed.getUTCFullYear() === Number(year) &&
    parsed.getUTCMonth() === Number(month) - 1 &&
    parsed.getUTCDate() === Number(day)
    ? `${year}-${month.padStart(2, "0")}-${day.padStart(2, "0")}`
    : "";
}

function positiveInteger(value: string) {
  const parsed = Number(value);
  return Number.isSafeInteger(parsed) && parsed > 0 ? String(parsed) : "";
}

function detailOutputPath(params: URLSearchParams, fallback: string) {
  return params.get("item") ? `/finance/reports/output?${params.toString()}` : fallback;
}

/**
 * Maps the explicit Report Item values used by the two legacy Finance
 * drill-down grids. The backend still owns the procedure allowlist; this is
 * only a compatibility translation from ParamNameN/ParamValueN query pairs.
 */
export function legacyFinanceDrillDownPath(
  query: LegacyFinanceQuery,
  source: "wesbank" | "regional",
) {
  const item = queryValue(query, "Item", "item").toLowerCase();
  const startDate = isoDate(numberedParameter(query, "startDate"));
  const endDate = isoDate(numberedParameter(query, "endDate"));
  const departmentCode = positiveInteger(numberedParameter(query, "departmentCode", "deptCode"));
  const siteCode = positiveInteger(numberedParameter(query, "siteID"));
  const province = numberedParameter(query, "province").trim();
  const registrationNumber = numberedParameter(query, "regNum").trim();
  const params = new URLSearchParams({ kind: "legacy-detail", format: "html" });
  if (startDate) params.set("startDate", startDate);
  if (endDate) params.set("endDate", endDate);
  if (departmentCode) params.set("departmentCode", departmentCode);
  if (siteCode) params.set("siteCode", siteCode);
  if (province) params.set("province", province);
  if (registrationNumber) params.set("registrationNumber", registrationNumber);

  if (source === "wesbank") {
    if (item === "sitevehicledetail") params.set("item", "wesbank-site-vehicle-detail");
    else if (item === "registrationnumberdetail")
      params.set("item", "wesbank-registration-number-detail");
    else if (item === "vehicledetail") params.set("item", "wesbank-vehicle-detail");
    return detailOutputPath(params, "/finance/wesbank");
  }

  if (item === "totalcostperprovince") params.set("item", "regional-total-cost-province");
  else if (item === "totalcostperprovinceperdepartment")
    params.set("item", "regional-total-cost-province-department");
  else if (item === "totalcostperprovinceperdepartmentpersite")
    params.set("item", "regional-total-cost-province-department-site");
  else if (item === "totalcostperprovinceperdepartmentpersitepercosttype")
    params.set("item", "regional-total-cost-province-department-site-cost-type");
  return detailOutputPath(params, "/finance/regional");
}

"use server";

import { revalidatePath } from "next/cache";
import { redirect } from "next/navigation";

import {
  createLeaseTariff,
  createLeaseTerm,
  getLeaseTerm,
  importLeaseTariffs,
  updateLeaseTariff,
  updateLeaseTerm,
  type LeaseTariffWriteInput,
  type LeaseTermRecord,
  type LeaseTermWriteInput,
} from "@/lib/api-fml";
import { getSession } from "@/lib/session";

const CONTRACT_MANAGEMENT_PERMISSION = BigInt(2);
const VEHICLE_MANAGEMENT_PERMISSION = BigInt(1);
const FINANCIAL_PERMISSION = BigInt(16);

class FmlValidationError extends Error {}

function getText(formData: FormData, name: string) {
  const value = formData.get(name);
  return typeof value === "string" ? value.trim() : "";
}

function getRequiredInteger(formData: FormData, name: string, label: string) {
  const value = getText(formData, name);
  const parsed = Number(value);
  if (!value || !Number.isSafeInteger(parsed) || parsed <= 0) {
    throw new FmlValidationError(`${label} is required.`);
  }
  return parsed;
}

function getOptionalInteger(formData: FormData, name: string, label: string) {
  const value = getText(formData, name);
  if (!value) return null;
  const parsed = Number(value);
  if (!Number.isSafeInteger(parsed)) throw new FmlValidationError(`${label} must be a whole number.`);
  return parsed;
}

function getOptionalDecimal(formData: FormData, name: string, label: string) {
  const value = getText(formData, name);
  if (!value) return null;
  const parsed = Number(value);
  if (!Number.isFinite(parsed)) throw new FmlValidationError(`${label} must be a valid amount.`);
  return parsed;
}

function getOptionalDate(formData: FormData, name: string, label: string) {
  const value = getText(formData, name);
  if (!value) return null;
  const date = new Date(`${value}T00:00:00Z`);
  if (Number.isNaN(date.getTime())) throw new FmlValidationError(`${label} must be a valid date.`);
  return value;
}

function getRequiredDate(formData: FormData, name: string, label: string) {
  const value = getOptionalDate(formData, name, label);
  if (!value) throw new FmlValidationError(`${label} is required.`);
  return value;
}

function getBoolean(formData: FormData, name: string) {
  return getText(formData, name).toLowerCase() === "true";
}

function hasPermission(accessLevel: string | undefined, permission: bigint) {
  if (!accessLevel) return false;
  try {
    return (BigInt(accessLevel) & permission) === permission;
  } catch {
    return false;
  }
}

async function authorize(permission: bigint) {
  const session = await getSession();
  if (session.status === "unavailable") return { ok: false as const, message: "The sign-in service is temporarily unavailable." };
  if (session.status !== "authenticated") return { ok: false as const, message: "Your session has expired. Sign in again." };
  if (!hasPermission(session.accessLevel, permission)) return { ok: false as const, message: "You do not have permission to maintain Full Maintenance Lease records." };
  return { ok: true as const, session };
}

function resultPath(path: string, result: string, message?: string) {
  const params = new URLSearchParams({ result });
  if (message) params.set("message", message);
  return `${path}?${params.toString()}`;
}

function apiMessage(error: unknown, fallback: string) {
  if (error instanceof Error && error.message) return error.message;
  return fallback;
}

function revalidateFmlRoutes() {
  revalidatePath("/full-maintenance-lease");
  revalidatePath("/full-maintenance-lease/tariffs");
  revalidatePath("/full-maintenance-lease/tariffs/details");
  revalidatePath("/full-maintenance-lease/add-lease");
  revalidatePath("/full-maintenance-lease/extend");
}

function readTermInput(formData: FormData, authorityStatus: number): LeaseTermWriteInput {
  const startDate = getOptionalDate(formData, "startDate", "Start date");
  const endDate = getOptionalDate(formData, "endDate", "End date");
  if (startDate && endDate && endDate < startDate) throw new FmlValidationError("End date cannot be earlier than the start date.");

  return {
    vmf_Code: getRequiredInteger(formData, "vmfCode", "Vehicle"),
    AgreedTerms: getOptionalInteger(formData, "agreedTerms", "Agreed terms"),
    AgreedKilos: getOptionalInteger(formData, "agreedKilos", "Agreed kilos"),
    AppliedInterest: getOptionalDecimal(formData, "appliedInterest", "Applied interest"),
    FixedMonthlyAmount: getOptionalDecimal(formData, "fixedMonthlyAmount", "Fixed monthly amount"),
    AuthorityStatus: authorityStatus,
    StartDate: startDate,
    EndDate: endDate,
    AgreedOverallKilo: getOptionalInteger(formData, "agreedOverallKilo", "Agreed overall kilos"),
    ExcessKilosTarrif: getOptionalDecimal(formData, "excessKilosTariff", "Excess kilos tariff"),
    RelieveVehicle: getBoolean(formData, "relieveVehicle"),
    lease_site_code: getOptionalInteger(formData, "leaseSiteCode", "Lease site"),
    authority_comment: getText(formData, "authorityComment") || null,
    rejection_reason: getText(formData, "rejectionReason") || null,
    lease_notes: getText(formData, "leaseNotes") || null,
  };
}

export async function createLeaseTermAction(formData: FormData) {
  const access = await authorize(CONTRACT_MANAGEMENT_PERMISSION);
  const path = "/full-maintenance-lease/add-lease";
  if (!access.ok) redirect(resultPath(path, "error", access.message));

  try {
    const created = await createLeaseTerm(readTermInput(formData, 1));
    if (!created) throw new FmlValidationError("The FIS API did not return the created lease term.");
  } catch (error) {
    redirect(resultPath(path, "error", apiMessage(error, "The lease term could not be created.")));
  }

  revalidateFmlRoutes();
  redirect(resultPath(path, "created"));
}

export async function saveLeaseTermAction(formData: FormData) {
  const access = await authorize(CONTRACT_MANAGEMENT_PERMISSION);
  const path = "/full-maintenance-lease/tariffs/details";
  const termId = getRequiredInteger(formData, "termId", "Lease term");
  if (!access.ok) redirect(resultPath(`${path}?id=${termId}`, "error", access.message));

  const operation = getText(formData, "operation");
  try {
    let updated: LeaseTermRecord | null;
    if (operation === "approve" || operation === "reject") {
      if (!hasPermission(access.session.accessLevel, FINANCIAL_PERMISSION)) {
        redirect(resultPath(`${path}?id=${termId}&mode=review`, "error", "You do not have financial authorisation for this workflow."));
      }

      const existing = await getLeaseTerm(termId);
      const currentUserCode = Number(access.session.userAccessCode);
      if (Number.isSafeInteger(currentUserCode) && currentUserCode > 0 && existing.createdByUserCode === currentUserCode) {
        redirect(resultPath(`${path}?id=${termId}&mode=review`, "error", "You cannot authorise a lease tariff that you captured yourself."));
      }

      const rejectionReason = getText(formData, "rejectionReason");
      if (operation === "reject" && !rejectionReason) {
        redirect(resultPath(`${path}?id=${termId}&mode=review`, "error", "Rejection reason is required."));
      }

      updated = await updateLeaseTerm(termId, {
        vmf_Code: existing.vmfCode,
        AgreedTerms: existing.agreedTerms,
        AgreedKilos: existing.agreedKilos,
        AppliedInterest: existing.appliedInterest,
        FixedMonthlyAmount: existing.fixedMonthlyAmount,
        AuthorityStatus: operation === "approve" ? 2 : 4,
        StartDate: existing.startDate,
        EndDate: existing.endDate,
        AgreedOverallKilo: existing.agreedOverallKilo,
        ExcessKilosTarrif: existing.excessKilosTariff,
        RelieveVehicle: existing.relieveVehicle,
        lease_site_code: existing.leaseSiteCode,
        authority_comment: getText(formData, "authorityComment") || existing.authorityComment,
        rejection_reason: rejectionReason || existing.rejectionReason,
        lease_notes: existing.comments,
      });
    } else {
      updated = await updateLeaseTerm(termId, readTermInput(formData, 1));
    }

    if (!updated) throw new FmlValidationError("The FIS API did not return the updated lease term.");
  } catch (error) {
    redirect(resultPath(`${path}?id=${termId}${operation === "approve" || operation === "reject" ? "&mode=review" : "&mode=edit"}`, "error", apiMessage(error, "The lease term could not be updated.")));
  }

  revalidateFmlRoutes();
  redirect(resultPath("/full-maintenance-lease/tariffs", operation === "approve" ? "approved" : operation === "reject" ? "rejected" : "updated"));
}

export async function createLeaseTariffAction(formData: FormData) {
  const access = await authorize(CONTRACT_MANAGEMENT_PERMISSION | VEHICLE_MANAGEMENT_PERMISSION);
  const path = "/full-maintenance-lease/add-lease";
  if (!access.ok) redirect(resultPath(path, "error", access.message));

  try {
    const startDate = getRequiredDate(formData, "startDate", "Start date");
    const endDate = getRequiredDate(formData, "endDate", "End date");
    if (endDate < startDate) throw new FmlValidationError("End date cannot be earlier than the start date.");
    const created = await createLeaseTariff({
      vmf_code: getRequiredInteger(formData, "vmfCode", "Vehicle"),
      start_date: startDate,
      end_date: endDate,
      fixed_tariff: getOptionalDecimal(formData, "fixedTariff", "Fixed tariff") ?? 0,
      excess_kilo_tariff: getOptionalDecimal(formData, "excessKiloTariff", "Excess kilo tariff"),
      active: true,
    });
    if (!created) throw new FmlValidationError("The FIS API did not return the created lease tariff.");
  } catch (error) {
    redirect(resultPath(path, "error", apiMessage(error, "The lease tariff could not be created.")));
  }

  revalidateFmlRoutes();
  redirect(resultPath(path, "created"));
}

export async function extendLeaseTariffAction(formData: FormData) {
  const access = await authorize(CONTRACT_MANAGEMENT_PERMISSION | VEHICLE_MANAGEMENT_PERMISSION);
  const path = "/full-maintenance-lease/extend";
  if (!access.ok) redirect(resultPath(path, "error", access.message));

  try {
    const startDate = getRequiredDate(formData, "startDate", "Start date");
    const endDate = getRequiredDate(formData, "endDate", "New end date");
    if (endDate < startDate) throw new FmlValidationError("The new end date cannot be earlier than the tariff start date.");
    const updated = await updateLeaseTariff(getRequiredInteger(formData, "leaseTariffCode", "Lease tariff"), {
      vmf_code: getRequiredInteger(formData, "vmfCode", "Vehicle"),
      start_date: startDate,
      end_date: endDate,
      fixed_tariff: getOptionalDecimal(formData, "fixedTariff", "Fixed tariff") ?? 0,
      excess_kilo_tariff: getOptionalDecimal(formData, "excessKiloTariff", "Excess kilo tariff"),
      active: true,
    });
    if (!updated) throw new FmlValidationError("The FIS API did not return the updated lease tariff.");
  } catch (error) {
    redirect(resultPath(path, "error", apiMessage(error, "The lease tariff could not be extended.")));
  }

  revalidateFmlRoutes();
  redirect(resultPath(path, "updated"));
}

function parseCsvRow(line: string) {
  const values: string[] = [];
  let value = "";
  let quoted = false;
  for (let index = 0; index < line.length; index += 1) {
    const character = line[index];
    if (character === '"') {
      if (quoted && line[index + 1] === '"') {
        value += '"';
        index += 1;
      } else {
        quoted = !quoted;
      }
    } else if (character === "," && !quoted) {
      values.push(value.trim());
      value = "";
    } else {
      value += character;
    }
  }
  values.push(value.trim());
  return values;
}

function normalizeHeader(value: string) {
  return value.replace(/[\s_-]/g, "").toLowerCase();
}

function csvNumber(value: string | undefined) {
  if (!value?.trim()) return null;
  const parsed = Number(value.trim());
  return Number.isFinite(parsed) ? parsed : null;
}

export async function importLeaseTariffsAction(formData: FormData) {
  const access = await authorize(CONTRACT_MANAGEMENT_PERMISSION | VEHICLE_MANAGEMENT_PERMISSION);
  const path = "/full-maintenance-lease/upload";
  if (!access.ok) redirect(resultPath(path, "error", access.message));

  const csv = getText(formData, "csv");
  const lines = csv.split(/\r?\n/).map((line) => line.trim()).filter(Boolean);
  if (lines.length < 2) redirect(resultPath(path, "error", "Provide a CSV header and at least one data row."));

  const headers = parseCsvRow(lines[0]).map(normalizeHeader);
  const indexOf = (...names: string[]) => names.map(normalizeHeader).map((name) => headers.indexOf(name)).find((index) => index >= 0) ?? -1;
  const vmfIndex = indexOf("vmf_code", "vmfcode");
  if (vmfIndex < 0) redirect(resultPath(path, "error", "CSV header must include VMF_Code."));

  let created = 0;
  let failed = 0;
  const rows = [] as Array<{
    vmfCode: number;
    ggNumber: string | null;
    gpNumber: string | null;
    startDate: string;
    endDate: string;
    fixedTariff: number;
    excessKiloTariff: number | null;
  }>;
  for (const line of lines.slice(1)) {
    const values = parseCsvRow(line);
    const vmfCode = csvNumber(values[vmfIndex]);
    const startDate = values[indexOf("start_date", "startdate")]?.trim();
    const endDate = values[indexOf("end_date", "enddate")]?.trim();
    const fixedTariff = csvNumber(values[indexOf("fixed_tariff", "fixedtariff", "leasetariff")]);
    const excessKiloTariff = csvNumber(values[indexOf("excess_kilo_tariff", "excesskilotariff")]);
    if (!vmfCode || !startDate || !endDate || fixedTariff === null) {
      failed += 1;
      continue;
    }

    if (endDate < startDate) {
      failed += 1;
      continue;
    }
    rows.push({
      vmfCode,
      ggNumber: values[indexOf("ggnumber", "gg_number")]?.trim() || null,
      gpNumber: values[indexOf("gpnumber", "gp_number")]?.trim() || null,
      startDate,
      endDate,
      fixedTariff,
      excessKiloTariff,
    });
  }

  try {
    if (rows.length > 0) {
      const result = await importLeaseTariffs(rows);
      created = result.imported;
      failed += result.failed;
    }
  } catch (error) {
    failed += rows.length;
    const message = apiMessage(error, "The lease tariff import could not be completed.");
    redirect(resultPath(path, "error", message));
  }

  revalidateFmlRoutes();
  const result = failed > 0 ? "error" : "imported";
  redirect(resultPath(path, result, `Import completed. Created: ${created}; failed: ${failed}.`));
}

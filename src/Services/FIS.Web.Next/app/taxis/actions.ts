"use server";

import { revalidatePath } from "next/cache";
import { redirect } from "next/navigation";

import {
  createTaxi,
  createTaxiWhiteLog,
  deleteTaxiScanDoc,
  getTaxi,
  saveTaxiLog,
  uploadTaxiScanDoc,
  updateTaxi,
  TaxiApiError,
  type TaxiInput,
  type TaxiLogInput,
} from "@/lib/api-taxis";
import { getSession } from "@/lib/session";

const TAXI_ROLE = "Private Hire Vehicles";

class TaxiValidationError extends Error {}

function text(formData: FormData, key: string) {
  const value = formData.get(key);
  return typeof value === "string" ? value.trim() : "";
}

function number(
  formData: FormData,
  key: string,
  label: string,
  options: { required?: boolean; integer?: boolean; min?: number } = {},
) {
  const value = text(formData, key);
  if (!value && !options.required) return null;
  const parsed = Number(value);
  const valid =
    Number.isFinite(parsed) &&
    (!options.integer || Number.isInteger(parsed)) &&
    (options.min === undefined || parsed >= options.min);
  if (!value || !valid) throw new TaxiValidationError(`${label} is invalid.`);
  return parsed;
}

function dateTime(
  formData: FormData,
  dateKey: string,
  timeKey: string,
  label: string,
  required = false,
) {
  const date = text(formData, dateKey);
  const time = text(formData, timeKey);
  if (!date && !time && !required) return null;
  if (!date || !time || Number.isNaN(Date.parse(`${date}T${time}:00`)))
    throw new TaxiValidationError(`${label} must be a valid date and time.`);
  return `${date}T${time}:00`;
}

function dateOnly(formData: FormData, key: string, label: string, required = false) {
  const value = text(formData, key);
  if (!value && !required) return null;
  if (!value || Number.isNaN(Date.parse(`${value}T00:00:00`)))
    throw new TaxiValidationError(`${label} must be a valid date.`);
  return `${value}T00:00:00`;
}

function safeReturnPath(formData: FormData, fallback: string) {
  const path = text(formData, "returnPath");
  return path.startsWith("/taxis") && !path.startsWith("//") ? path : fallback;
}

function redirectWithMessage(path: string, key: "saved" | "error", message: string) {
  const query = new URLSearchParams({ [key]: message });
  redirect(`${path}${path.includes("?") ? "&" : "?"}${query.toString()}`);
}

async function authorizeTaxi() {
  const session = await getSession();
  if (session.status !== "authenticated")
    return "Your session has expired. Sign in again before continuing.";
  if (
    !session.roles.some(
      (role) => role.localeCompare(TAXI_ROLE, undefined, { sensitivity: "accent" }) === 0,
    )
  )
    return "You do not have permission to use Taxi Maintenance.";
  return null;
}

function apiErrorMessage(error: unknown, subject: string) {
  if (error instanceof TaxiApiError) {
    if (error.reason === "unauthorized")
      return "Your session has expired. Sign in again before continuing.";
    if (error.reason === "unavailable")
      return `The Taxi ${subject} service is temporarily unavailable. Please try again.`;
    if (error.reason === "not-found") return `The ${subject} record was not found.`;
  }
  return `The Taxi ${subject} operation failed. Please try again.`;
}

function existingTaxiInput(taxi: Awaited<ReturnType<typeof getTaxi>>): TaxiInput {
  const { requestId, dateCreated, dateUpdated, departmentName, siteName, ...fields } = taxi;
  return { ...fields, requestId };
}

function taxiInput(formData: FormData, existing?: Awaited<ReturnType<typeof getTaxi>>): TaxiInput {
  const rekNum = text(formData, "rekNum");
  const siteCode = number(formData, "siteCode", "Site code", {
    required: true,
    integer: true,
    min: 1,
  });
  const required = dateTime(
    formData,
    "dateRequired",
    "timeRequired",
    "Transport date and time",
    true,
  );
  const official = text(formData, "official");
  if (!rekNum) throw new TaxiValidationError("Requisition number is required.");
  if (!official) throw new TaxiValidationError("Official/passenger name is required.");

  const input: TaxiInput = {
    requestId: existing?.requestId,
    rekNum,
    contractorId: number(formData, "contractorId", "Service provider", { integer: true, min: 1 }),
    vmfCode: text(formData, "vmfCode") || null,
    departmentCode: number(formData, "departmentCode", "Department code", {
      integer: true,
      min: 1,
    }),
    siteCode: siteCode ?? 0,
    dateRequired: required ?? "",
    timeRequired: required ?? "",
    vehicleTypeCode: number(formData, "vehicleTypeCode", "Vehicle class", {
      integer: true,
      min: 0,
    }),
    official,
    rank: text(formData, "rank") || null,
    confirmed: existing?.confirmed ?? null,
    subContractorId: existing?.subContractorId ?? null,
    cancelled: existing?.cancelled ?? null,
    driver: text(formData, "driver") || null,
    regNum: text(formData, "regNum") || null,
    parentTaxiCode: existing?.parentTaxiCode ?? null,
    address1: text(formData, "address1") || null,
    address2: text(formData, "address2") || null,
    address3: text(formData, "address3") || null,
    flight: text(formData, "flight") || null,
    instructions: text(formData, "instructions") || null,
    destination1: text(formData, "destination1") || null,
    destination2: text(formData, "destination2") || null,
    destination3: text(formData, "destination3") || null,
    userAccessCode: existing?.userAccessCode ?? null,
    requestDate: existing?.requestDate ?? null,
    respCode: existing?.respCode ?? null,
    objectCode: existing?.objectCode ?? null,
    fmsCode: existing?.fmsCode ?? null,
    dateRequired2: existing?.dateRequired2 ?? null,
    timeRequired2: existing?.timeRequired2 ?? null,
    address12: existing?.address12 ?? null,
    address22: existing?.address22 ?? null,
    address32: existing?.address32 ?? null,
    destination12: existing?.destination12 ?? null,
    destination22: existing?.destination22 ?? null,
    destination32: existing?.destination32 ?? null,
    transManName: existing?.transManName ?? null,
    transManDate: existing?.transManDate ?? null,
    transManRank: existing?.transManRank ?? null,
    transManTel: existing?.transManTel ?? null,
    bookingBy: existing?.bookingBy ?? null,
    arrivalTime: existing?.arrivalTime ?? null,
    project: existing?.project ?? null,
    driverAvailable: formData.get("driverAvailable") === "on",
    persal: existing?.persal ?? null,
    jiaPickup: formData.get("jiaPickup") === "on",
    officialTelNum: existing?.officialTelNum ?? null,
    fundCode: existing?.fundCode ?? null,
  };
  return input;
}

export async function saveTaxiRequestAction(formData: FormData) {
  const returnPath = safeReturnPath(formData, "/taxis/requests");
  const accessError = await authorizeTaxi();
  if (accessError) redirectWithMessage(returnPath, "error", accessError);
  try {
    const requestId = number(formData, "requestId", "Request ID", { integer: true, min: 1 });
    const existing = requestId ? await getTaxi(requestId) : undefined;
    const input = taxiInput(formData, existing);
    const saved = requestId ? await updateTaxi(requestId, input) : await createTaxi(input);
    revalidatePath("/taxis");
    revalidatePath("/taxis/requests");
    redirectWithMessage(
      `${returnPath}?mode=${requestId ? "edit" : "add"}`,
      "saved",
      `Taxi requisition ${saved.rekNum} saved.`,
    );
  } catch (error) {
    if (error instanceof Error && !(error instanceof TaxiApiError))
      redirectWithMessage(returnPath, "error", error.message);
    redirectWithMessage(returnPath, "error", apiErrorMessage(error, "requisition"));
  }
}

export async function cancelTaxiRequestAction(formData: FormData) {
  const returnPath = safeReturnPath(formData, "/taxis/requests/cancel");
  const accessError = await authorizeTaxi();
  if (accessError) redirectWithMessage(returnPath, "error", accessError);
  try {
    const requestId = number(formData, "requestId", "Request ID", {
      required: true,
      integer: true,
      min: 1,
    });
    const taxi = await getTaxi(requestId ?? 0);
    const reason = text(formData, "cancelReason") || "Cancelled by user";
    await updateTaxi(requestId ?? 0, { ...existingTaxiInput(taxi), cancelled: reason });
    revalidatePath("/taxis");
    redirectWithMessage(returnPath, "saved", `Taxi requisition ${taxi.rekNum} cancelled.`);
  } catch (error) {
    redirectWithMessage(
      returnPath,
      "error",
      error instanceof TaxiValidationError ? error.message : apiErrorMessage(error, "cancellation"),
    );
  }
}

function taxiLogInput(formData: FormData): TaxiLogInput {
  const requestId = number(formData, "requestId", "Request ID", {
    required: true,
    integer: true,
    min: 1,
  });
  const contractorId = number(formData, "contractorId", "Service provider", {
    required: true,
    integer: true,
    min: 1,
  });
  const driverStartOdo = number(formData, "driverStartOdo", "Driver start odometer", {
    required: true,
    min: 0,
  });
  const driverEndOdo = number(formData, "driverEndOdo", "Driver end odometer", {
    required: true,
    min: 0,
  });
  const startDate = dateOnly(formData, "driverStartDate", "Driver start date", true);
  const endDate = dateOnly(formData, "driverEndDate", "Driver end date", true);
  const driverStartTime = text(formData, "driverStartTime");
  const driverEndTime = text(formData, "driverEndTime");
  if (
    !requestId ||
    !contractorId ||
    driverStartOdo === null ||
    driverEndOdo === null ||
    !startDate ||
    !endDate ||
    !driverStartTime ||
    !driverEndTime
  )
    throw new TaxiValidationError("Complete all required taxi-log fields.");
  if (driverEndOdo <= driverStartOdo)
    throw new TaxiValidationError(
      "Driver end odometer must be greater than driver start odometer.",
    );
  const driver = text(formData, "driver");
  const registrationNumber = text(formData, "registrationNumber");
  if (!driver || !registrationNumber)
    throw new TaxiValidationError("Driver and registration number are required.");
  return {
    requestId,
    rekNum: text(formData, "rekNum"),
    contractorId,
    vehicleTypeCode: number(formData, "vehicleTypeCode", "Vehicle class", {
      integer: true,
      min: 0,
    }),
    registrationNumber,
    driver,
    driverStartOdo,
    driverEndOdo,
    driverStartDate: startDate,
    driverEndDate: endDate,
    driverStartTime,
    driverEndTime,
    taxiLogNoteCode: number(formData, "taxiLogNoteCode", "Log note", { integer: true, min: 0 }),
    quotedTariff: number(formData, "quotedTariff", "Quoted tariff", { min: 0 }),
  };
}

export async function saveTaxiLogAction(formData: FormData) {
  const returnPath = safeReturnPath(formData, "/taxis/logs/enter");
  const accessError = await authorizeTaxi();
  if (accessError) redirectWithMessage(returnPath, "error", accessError);
  try {
    const input = taxiLogInput(formData);
    const logId = number(formData, "logId", "Log ID", { integer: true, min: 1 });
    await saveTaxiLog(input, logId ?? undefined);
    revalidatePath("/taxis/logs/enter");
    revalidatePath("/taxis/logs/edit");
    redirectWithMessage(returnPath, "saved", `Taxi log ${input.rekNum} saved.`);
  } catch (error) {
    redirectWithMessage(
      returnPath,
      "error",
      error instanceof TaxiValidationError ? error.message : apiErrorMessage(error, "log"),
    );
  }
}

export async function saveTaxiWhiteLogAction(formData: FormData) {
  const returnPath = safeReturnPath(formData, "/taxis/logs/white-log");
  const accessError = await authorizeTaxi();
  if (accessError) redirectWithMessage(returnPath, "error", accessError);
  try {
    const vmfCode = number(formData, "vmfCode", "GG number", {
      required: true,
      integer: true,
      min: 1,
    });
    const startOdo = number(formData, "startOdo", "Start odometer", {
      required: true,
      integer: true,
      min: 0,
    });
    const endOdo = number(formData, "endOdo", "End odometer", {
      required: true,
      integer: true,
      min: 0,
    });
    const startDate = dateOnly(formData, "startDate", "Start date", true);
    const endDate = dateOnly(formData, "endDate", "End date", true);
    const driver = text(formData, "driver");
    if (
      vmfCode === null ||
      startOdo === null ||
      endOdo === null ||
      !startDate ||
      !endDate ||
      !driver
    )
      throw new TaxiValidationError("Complete all required white-log fields.");
    if (endOdo <= startOdo)
      throw new TaxiValidationError("End odometer must be greater than start odometer.");
    await createTaxiWhiteLog({ vmfCode, startOdo, endOdo, startDate, endDate, driver });
    revalidatePath("/taxis/logs/white-log");
    redirectWithMessage(returnPath, "saved", "Taxi white log saved.");
  } catch (error) {
    redirectWithMessage(
      returnPath,
      "error",
      error instanceof TaxiValidationError ? error.message : apiErrorMessage(error, "white log"),
    );
  }
}

export async function uploadTaxiScanDocAction(formData: FormData) {
  const returnPath = safeReturnPath(formData, "/taxis/scan-requisition");
  const accessError = await authorizeTaxi();
  if (accessError) redirectWithMessage(returnPath, "error", accessError);
  try {
    const vmfCode = number(formData, "vmfCode", "Vehicle", {
      required: true,
      integer: true,
      min: 1,
    });
    const begin = dateOnly(formData, "periodBegin", "Certificate begin date", true);
    const end = dateOnly(formData, "periodEnd", "Certificate end date", true);
    const file = formData.get("file");
    if (vmfCode === null || !begin || !end || !(file instanceof File) || file.size === 0) {
      throw new TaxiValidationError("Choose a vehicle, certificate period, and image scan.");
    }
    if (file.size > 20 * 1024 * 1024)
      throw new TaxiValidationError("The scan must be 20 MB or smaller.");
    await uploadTaxiScanDoc({ vmfCode, periodBegin: begin, periodEnd: end, file });
    revalidatePath("/taxis/scan-requisition");
    redirectWithMessage(returnPath, "saved", "Taxi requisition certificate uploaded.");
  } catch (error) {
    redirectWithMessage(
      returnPath,
      "error",
      error instanceof TaxiValidationError
        ? error.message
        : apiErrorMessage(error, "requisition scan"),
    );
  }
}

export async function deleteTaxiScanDocAction(formData: FormData) {
  const returnPath = safeReturnPath(formData, "/taxis/scan-requisition");
  const accessError = await authorizeTaxi();
  if (accessError) redirectWithMessage(returnPath, "error", accessError);
  try {
    const scanDocCode = number(formData, "scanDocCode", "Scan document", {
      required: true,
      integer: true,
      min: 1,
    });
    await deleteTaxiScanDoc(scanDocCode ?? 0);
    revalidatePath("/taxis/scan-requisition");
    redirectWithMessage(returnPath, "saved", "Taxi requisition certificate deleted.");
  } catch (error) {
    redirectWithMessage(
      returnPath,
      "error",
      error instanceof TaxiValidationError
        ? error.message
        : apiErrorMessage(error, "requisition scan deletion"),
    );
  }
}

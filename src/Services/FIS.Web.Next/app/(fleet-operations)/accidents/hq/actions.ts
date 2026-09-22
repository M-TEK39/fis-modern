"use server";

import { revalidatePath } from "next/cache";
import { redirect } from "next/navigation";

import {
  AccidentApiError,
  createAccidentAgainstApi,
  getAccidentForEdit,
  updateHqAccidentAgainstApi,
  type AccidentEditRecord,
  type AccidentUpdateRequest,
  type CreateAccidentRequest,
} from "@/lib/api/fleet-operations/api-accidents";
import { accidentFinancialYearChoices } from "@/app/(fleet-operations)/accidents/_financial-years";
import { getSession } from "@/lib/auth/session";

const ACCIDENTS_ROLE = "Accidents";

export type HqAccidentActionState = {
  status: "idle" | "error";
  message?: string;
};

const initialStatus: HqAccidentActionState = { status: "idle" };

class AccidentFormValidationError extends Error {}

function getText(formData: FormData, key: string) {
  const value = formData.get(key);
  return typeof value === "string" ? value.trim() : "";
}

function getAccidentCode(formData: FormData) {
  const value = getText(formData, "accidentCode");
  const parsed = Number(value);
  if (!value || !Number.isInteger(parsed) || parsed <= 0) {
    throw new AccidentFormValidationError("A valid accident record is required.");
  }

  return parsed;
}

function getRequiredText(formData: FormData, key: string, label: string, maxLength: number) {
  const value = getText(formData, key);
  if (!value) {
    throw new AccidentFormValidationError(`${label} is required.`);
  }

  if (value.length > maxLength) {
    throw new AccidentFormValidationError(`${label} must be ${maxLength} characters or fewer.`);
  }

  return value;
}

function getOptionalText(formData: FormData, key: string, label: string, maxLength: number) {
  const value = getText(formData, key);
  if (value.length > maxLength) {
    throw new AccidentFormValidationError(`${label} must be ${maxLength} characters or fewer.`);
  }

  return value || null;
}

function getPositiveInteger(formData: FormData, key: string, label: string) {
  const value = getText(formData, key);
  const parsed = Number(value);
  if (!value || !Number.isInteger(parsed) || parsed <= 0) {
    throw new AccidentFormValidationError(`${label} is required.`);
  }

  return parsed;
}

function getDate(formData: FormData, key: string, label: string) {
  const value = getText(formData, key);
  const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(value);
  if (!match) {
    throw new AccidentFormValidationError(`${label} is required.`);
  }

  const year = Number(match[1]);
  const month = Number(match[2]);
  const day = Number(match[3]);
  const parsed = new Date(Date.UTC(year, month - 1, day));
  if (
    parsed.getUTCFullYear() !== year ||
    parsed.getUTCMonth() !== month - 1 ||
    parsed.getUTCDate() !== day
  ) {
    throw new AccidentFormValidationError(`${label} is invalid.`);
  }

  return `${value}T00:00:00.000Z`;
}

function getOptionalDate(formData: FormData, key: string, label: string) {
  const value = getText(formData, key);
  if (!value) {
    return null;
  }

  const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(value);
  if (!match) {
    throw new AccidentFormValidationError(`${label} is invalid.`);
  }

  const year = Number(match[1]);
  const month = Number(match[2]);
  const day = Number(match[3]);
  const parsed = new Date(Date.UTC(year, month - 1, day));
  if (
    parsed.getUTCFullYear() !== year ||
    parsed.getUTCMonth() !== month - 1 ||
    parsed.getUTCDate() !== day
  ) {
    throw new AccidentFormValidationError(`${label} is invalid.`);
  }

  return `${value}T00:00:00.000Z`;
}

function getOptionalInteger(formData: FormData, key: string, label: string) {
  const value = getText(formData, key);
  if (!value) {
    return null;
  }

  const parsed = Number(value);
  if (!Number.isInteger(parsed) || parsed < 0) {
    throw new AccidentFormValidationError(`${label} must be a whole number.`);
  }

  return parsed;
}

function getChoice(formData: FormData, key: string, label: string, allowed: readonly string[]) {
  const value = getText(formData, key);
  if (!value) {
    return null;
  }

  if (!allowed.includes(value)) {
    throw new AccidentFormValidationError(`${label} has an invalid value.`);
  }

  return value;
}

function getRequiredChoice(
  formData: FormData,
  key: string,
  label: string,
  allowed: readonly string[],
) {
  const value = getChoice(formData, key, label, allowed);
  if (!value) {
    throw new AccidentFormValidationError(`${label} is required.`);
  }

  return value;
}

function getAmount(formData: FormData, key: string, label: string) {
  const value = getText(formData, key) || "0";
  const parsed = Number(value);
  if (!Number.isFinite(parsed) || parsed < 0) {
    throw new AccidentFormValidationError(`${label} must be a non-negative amount.`);
  }

  return parsed;
}

function getOptionalAmount(
  formData: FormData,
  key: string,
  label: string,
  existing: number | null,
) {
  const value = getText(formData, key);
  if (!value && existing === null) {
    return null;
  }

  const parsed = Number(value || "0");
  if (!Number.isFinite(parsed) || parsed < 0) {
    throw new AccidentFormValidationError(`${label} must be a non-negative amount.`);
  }

  return parsed;
}

function getOptionalTime(formData: FormData, key: string, accidentDate: string) {
  const value = getText(formData, key);
  if (!value) {
    return null;
  }

  if (!/^(?:[01]\d|2[0-3]):[0-5]\d$/.test(value)) {
    throw new AccidentFormValidationError("Accident time must use the HH:MM format.");
  }

  return `${accidentDate.slice(0, 10)}T${value}:00.000Z`;
}

const CAPTURE_PERSONS = ["?", "HM", "DF", "MDS", "CR", "JR", "MO", "AJ"] as const;

function buildCreateRequest(formData: FormData): CreateAccidentRequest {
  const vmfCode = getPositiveInteger(formData, "vmfCode", "Vehicle");
  const occurrenceDate = getDate(formData, "occurenceDate", "Accident date");
  const accidentTime = getOptionalTime(formData, "occurenceTime", occurrenceDate);
  const driverEmployNumber = getOptionalText(
    formData,
    "driverEmployNumber",
    "Driver ID number",
    13,
  );
  if (driverEmployNumber && !/^[0-9/]+$/.test(driverEmployNumber)) {
    throw new AccidentFormValidationError("Driver ID number may contain only numbers and '/'.");
  }

  return {
    vmf_code: vmfCode,
    description: getRequiredText(formData, "description", "Accident description", 60),
    driver_name: getOptionalText(formData, "driverName", "GG driver name", 25),
    driver_employ_number: driverEmployNumber,
    hq_reference: getOptionalText(formData, "hqReference", "HQ reference", 20),
    gg_reference: getOptionalText(formData, "ggReference", "GG reference", 20),
    sa_reference: getOptionalText(formData, "saReference", "SA reference", 20),
    occurence_date: occurrenceDate,
    occurence_time: accidentTime,
    reported_date: occurrenceDate,
    claim_amount: getAmount(formData, "claimAmount", "Claim amount"),
    excess_amount: 0,
    call_refer: null,
    captured_person: getChoice(formData, "capturedPerson", "Capture person", CAPTURE_PERSONS),
    fin_year: getRequiredChoice(
      formData,
      "finYear",
      "Financial year",
      accidentFinancialYearChoices(),
    ),
    garage: getChoice(formData, "garage", "Garage", ["PTA", "JHB"]),
    driver_telno: null,
    driver_site_code: getOptionalInteger(formData, "driverSiteCode", "Site"),
    transoffic_name: getOptionalText(formData, "transportOfficerName", "Transport officer", 30),
    transoffic_tel: getOptionalText(
      formData,
      "transportOfficerTel",
      "Transport officer telephone",
      20,
    ),
    accident_km: getAmount(formData, "accidentKm", "GG car km"),
    acc_type_code: null,
    flag_gg_hq: null,
    flag_gg_hq_date: null,
    file_close_date: null,
    case_number: getOptionalText(formData, "caseNumber", "Case number", 15),
    reporting_authority: null,
    cost_of_repair: getAmount(formData, "costOfRepair", "GG car damage"),
    damage_description: getOptionalText(formData, "damageDescription", "GG damage description", 60),
    death: getChoice(formData, "death", "Death?", ["?", "N", "Y"]),
    injured: getChoice(formData, "injured", "Injured?", ["?", "N", "Y"]),
    third_party_regno: getOptionalText(
      formData,
      "thirdPartyRegistration",
      "Private party registration",
      8,
    ),
    third_party_owner: getOptionalText(formData, "thirdPartyOwner", "Private party name", 30),
    third_party_tel: null,
    third_party_claim: getAmount(formData, "thirdPartyClaim", "Private car damage"),
    SecondThirdPartyRegNo: null,
    th_claim_receive: getChoice(formData, "claimReceived", "Claim received", ["N", "Y"]),
    claim_against_dept: getAmount(formData, "claimAmount", "Claim amount"),
    letterhead: null,
    z181: null,
    part3: null,
    statement: null,
    sketch: null,
    iddoc: null,
    drivelic: null,
    docs_acc_relieve: null,
    flag_case_num: null,
    trip_author: getChoice(formData, "tripAuthor", "Trip authority", ["Y", "N"]),
    flag_trip_author: null,
    flag_trip_auth_date: null,
    driver_fault: getChoice(formData, "driverFault", "GG driver fault", [
      "Unknown",
      "Yes",
      "No",
      "Maybe",
    ]),
    attorney_insure: getChoice(formData, "attorneyInsure", "Attorney / insurance", [
      "?",
      "ATT",
      "INS",
    ]),
    insurance_claim: getChoice(formData, "insuranceClaim", "Claim against department", [
      "?",
      "Y",
      "N",
    ]),
    priv_dampay_date: getOptionalDate(
      formData,
      "privateDamagePaymentDate",
      "Private damage payment date",
    ),
    th_claim_accept_reject: getChoice(formData, "thirdPartyClaimDecision", "Claim accept/reject", [
      "?",
      "ACC",
      "REJ",
    ]),
    th_claim_reject_reason: getOptionalText(
      formData,
      "thirdPartyClaimRejectReason",
      "Claim reject reason",
      30,
    ),
    write_off_amount: getAmount(formData, "writeOffAmount", "Write-off amount"),
    write_off_date: null,
    occurence_place: getOptionalText(formData, "occurencePlace", "Accident place", 50),
    tow_need: null,
    notes: getOptionalText(formData, "notes", "Notes", 60),
  };
}

function buildUpdateRequest(
  formData: FormData,
  existing: AccidentEditRecord,
): AccidentUpdateRequest {
  const occurrenceDate = getDate(formData, "occurenceDate", "Accident date");
  const driverEmployNumber = getOptionalText(
    formData,
    "driverEmployNumber",
    "Driver ID number",
    13,
  );
  if (driverEmployNumber && !/^[0-9/]+$/.test(driverEmployNumber)) {
    throw new AccidentFormValidationError("Driver ID number may contain only numbers and '/'.");
  }

  return {
    accident_code: existing.accidentCode,
    vmf_code: existing.vmfCode,
    posting_month_code: existing.postingMonthCode,
    description: getRequiredText(formData, "description", "Accident description", 60),
    driver_name: getOptionalText(formData, "driverName", "GG driver name", 25),
    driver_employ_number: driverEmployNumber,
    hq_reference: getOptionalText(formData, "hqReference", "HQ reference", 20),
    gg_reference: getOptionalText(formData, "ggReference", "GG reference", 20),
    sa_reference: getOptionalText(formData, "saReference", "SA reference", 20),
    occurence_date: occurrenceDate,
    occurence_time: getOptionalTime(formData, "occurenceTime", occurrenceDate),
    reported_date: existing.reportedDate,
    claim_amount: getAmount(formData, "claimAmount", "Claim amount"),
    excess_amount: existing.excessAmount,
    date_created: existing.dateCreated,
    date_updated: existing.dateUpdated,
    created_by_user_code: existing.createdByUserCode,
    modified_by_user_code: existing.modifiedByUserCode,
    is_deleted: existing.isDeleted,
    call_refer: existing.callRefer,
    captured_person: getChoice(formData, "capturedPerson", "Capture person", CAPTURE_PERSONS),
    fin_year: getRequiredChoice(
      formData,
      "finYear",
      "Financial year",
      accidentFinancialYearChoices(existing.finYear),
    ),
    garage: getChoice(formData, "garage", "Garage", ["PTA", "JHB"]),
    driver_telno: existing.driverTelno,
    driver_site_code: getOptionalInteger(formData, "driverSiteCode", "Site"),
    transoffic_name: getOptionalText(formData, "transportOfficerName", "Transport officer", 30),
    transoffic_tel: getOptionalText(
      formData,
      "transportOfficerTel",
      "Transport officer telephone",
      20,
    ),
    accident_km: getAmount(formData, "accidentKm", "GG car km"),
    acc_type_code: existing.accidentTypeCode,
    flag_gg_hq: existing.flagGgHq,
    flag_gg_hq_date: existing.flagGgHqDate,
    file_close_date: existing.fileCloseDate,
    case_number: getOptionalText(formData, "caseNumber", "Case number", 15),
    reporting_authority: existing.reportingAuthority,
    cost_of_repair: getAmount(formData, "costOfRepair", "GG car damage"),
    damage_description: getOptionalText(formData, "damageDescription", "GG damage description", 60),
    death: getChoice(formData, "death", "Death?", ["?", "N", "Y"]),
    injured: getChoice(formData, "injured", "Injured?", ["?", "N", "Y"]),
    third_party_regno: getOptionalText(
      formData,
      "thirdPartyRegistration",
      "Private party registration",
      8,
    ),
    third_party_owner: getOptionalText(formData, "thirdPartyOwner", "Private party name", 30),
    third_party_tel: existing.thirdPartyTelephone,
    third_party_claim: getAmount(formData, "thirdPartyClaim", "Private car damage"),
    SecondThirdPartyRegNo: existing.secondThirdPartyRegNo,
    th_claim_receive: getChoice(formData, "claimReceived", "Claim received", ["N", "Y"]),
    claim_against_dept: getAmount(formData, "claimAmount", "Claim amount"),
    letterhead: existing.letterhead,
    z181: existing.z181,
    part3: existing.part3,
    statement: existing.statement,
    sketch: existing.sketch,
    iddoc: existing.iddoc,
    drivelic: existing.drivelic,
    docs_acc_relieve: existing.documentsAccidentRelieve,
    flag_case_num: existing.flagCaseNumber,
    trip_author: getChoice(formData, "tripAuthor", "Trip authority", ["Y", "N"]),
    flag_trip_author: existing.flagTripAuthor,
    flag_trip_auth_date: existing.flagTripAuthDate,
    driver_fault: getChoice(formData, "driverFault", "GG driver fault", [
      "Unknown",
      "Yes",
      "No",
      "Maybe",
    ]),
    attorney_insure: getChoice(formData, "attorneyInsure", "Attorney / insurance", [
      "?",
      "ATT",
      "INS",
    ]),
    insurance_claim: getChoice(formData, "insuranceClaim", "Claim against department", [
      "?",
      "Y",
      "N",
    ]),
    priv_dampay_date: existing.privateDamagePaymentDate,
    th_claim_accept_reject: getChoice(formData, "thirdPartyClaimDecision", "Claim accept/reject", [
      "?",
      "ACC",
      "REJ",
    ]),
    th_claim_reject_reason: getOptionalText(
      formData,
      "thirdPartyClaimRejectReason",
      "Claim reject reason",
      30,
    ),
    write_off_amount: getOptionalAmount(
      formData,
      "writeOffAmount",
      "Write-off amount",
      existing.writeOffAmount,
    ),
    write_off_date: existing.writeOffDate,
    occurence_place: getOptionalText(formData, "occurencePlace", "Accident place", 50),
    tow_need: existing.towNeed,
    notes: getOptionalText(formData, "notes", "Notes", 60),
  };
}

function hasRole(roles: readonly string[], role: string) {
  return roles.some(
    (candidate) => candidate.localeCompare(role, undefined, { sensitivity: "accent" }) === 0,
  );
}

function apiErrorMessage(error: AccidentApiError, operation: "add" | "edit") {
  if (error.reason === "unauthorized") {
    return `Your session has expired or you are no longer allowed to ${operation} HQ accidents. Sign in again.`;
  }

  if (error.reason === "not-found") {
    return "That accident record no longer exists.";
  }

  if (error.reason === "unavailable") {
    return "The accident service is temporarily unavailable. Please try again.";
  }

  return "The accident service rejected the submitted data. Check the fields and try again.";
}

async function getAuthorizedSession(operation: "add" | "edit") {
  const session = await getSession();
  if (session.status === "unavailable") {
    return { error: `The sign-in service is temporarily unavailable. Please try again.` } as const;
  }

  if (session.status !== "authenticated") {
    return {
      error: `Your session has expired. Sign in again before ${operation === "add" ? "adding" : "editing"} an accident.`,
    } as const;
  }

  if (!hasRole(session.roles, ACCIDENTS_ROLE)) {
    return { error: `You do not have permission to ${operation} HQ accidents.` } as const;
  }

  return { session } as const;
}

export async function createHqAccidentAction(
  _previousState: HqAccidentActionState = initialStatus,
  formData: FormData,
): Promise<HqAccidentActionState> {
  let request: CreateAccidentRequest;
  try {
    request = buildCreateRequest(formData);
  } catch (error) {
    return {
      status: "error",
      message:
        error instanceof AccidentFormValidationError
          ? error.message
          : "Check the submitted fields.",
    };
  }

  const authorization = await getAuthorizedSession("add");
  if ("error" in authorization) {
    return { status: "error", message: authorization.error };
  }

  try {
    await createAccidentAgainstApi(request);
  } catch (error) {
    if (error instanceof AccidentApiError) {
      return { status: "error", message: apiErrorMessage(error, "add") };
    }

    console.error(
      "FIS HQ accident creation failed",
      error instanceof Error ? error.message : "unknown error",
    );
    return { status: "error", message: "Accident creation failed. Please try again." };
  }

  revalidatePath("/accidents/hq");
  redirect("/accidents/hq?created=1");
}

export async function updateHqAccidentAction(
  _previousState: HqAccidentActionState = initialStatus,
  formData: FormData,
): Promise<HqAccidentActionState> {
  const authorization = await getAuthorizedSession("edit");
  if ("error" in authorization) {
    return { status: "error", message: authorization.error };
  }

  let accidentCode: number;
  try {
    accidentCode = getAccidentCode(formData);
  } catch (error) {
    return {
      status: "error",
      message:
        error instanceof AccidentFormValidationError
          ? error.message
          : "A valid accident record is required.",
    };
  }

  try {
    const existing = await getAccidentForEdit(accidentCode);
    await updateHqAccidentAgainstApi(buildUpdateRequest(formData, existing));
  } catch (error) {
    if (error instanceof AccidentFormValidationError) {
      return { status: "error", message: error.message };
    }

    if (error instanceof AccidentApiError) {
      return { status: "error", message: apiErrorMessage(error, "edit") };
    }

    console.error(
      "FIS HQ accident update failed",
      error instanceof Error ? error.message : "unknown error",
    );
    return { status: "error", message: "Accident update failed. Please try again." };
  }

  revalidatePath("/accidents/hq");
  redirect("/accidents/hq?updated=1");
}

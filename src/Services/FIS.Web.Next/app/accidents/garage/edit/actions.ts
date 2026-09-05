"use server";

import { revalidatePath } from "next/cache";
import { redirect } from "next/navigation";

import {
  AccidentApiError,
  getAccidentForEdit,
  updateAccidentAgainstApi,
  type AccidentEditRecord,
  type AccidentUpdateRequest,
} from "@/lib/api-accidents";
import { getSession } from "@/lib/session";

const ACCIDENTS_ROLE = "Accidents";

export type GarageEditActionState = {
  status: "idle" | "error";
  message?: string;
};

const initialStatus: GarageEditActionState = { status: "idle" };

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

function getRequiredDate(formData: FormData, key: string, label: string) {
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

function getAmount(formData: FormData, key: string, label: string, existing: number | null) {
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

function hasRole(roles: readonly string[], role: string) {
  return roles.some((candidate) => candidate.localeCompare(role, undefined, { sensitivity: "accent" }) === 0);
}

function buildUpdateRequest(formData: FormData, existing: AccidentEditRecord): AccidentUpdateRequest {
  const accidentDate = getRequiredDate(formData, "occurenceDate", "Accident date");
  const reportedDate = getRequiredDate(formData, "reportedDate", "Date reported");
  const driverEmployNumber = getOptionalText(formData, "driverEmployNumber", "Driver ID number", 13);
  if (driverEmployNumber && !/^[0-9/]+$/.test(driverEmployNumber)) {
    throw new AccidentFormValidationError("Driver ID number may contain only numbers and '/'.");
  }

  const flagGgHq = getChoice(formData, "flagGgHq", "Notify HQ", ["N", "Y", "X"]);
  const flagGgHqDate = getOptionalDate(formData, "flagGgHqDate", "Notify HQ date");
  if ((flagGgHq === "Y" || flagGgHq === "X") && !flagGgHqDate) {
    throw new AccidentFormValidationError("Notify HQ date is required when Notify HQ is Y or X.");
  }

  const flagTripAuthor = getChoice(formData, "flagTripAuthor", "Notify trip authority", ["N", "Y", "X"]);
  const flagTripAuthDate = getOptionalDate(formData, "flagTripAuthDate", "Notify trip date");
  if ((flagTripAuthor === "Y" || flagTripAuthor === "X") && !flagTripAuthDate) {
    throw new AccidentFormValidationError("Notify trip date is required when Notify trip authority is Y or X.");
  }

  return {
    accident_code: existing.accidentCode,
    vmf_code: existing.vmfCode,
    posting_month_code: existing.postingMonthCode,
    description: getRequiredText(formData, "description", "Accident description", 60),
    driver_name: getOptionalText(formData, "driverName", "GG driver name", 25),
    driver_employ_number: driverEmployNumber,
    hq_reference: getOptionalText(formData, "hqReference", "HQ reference", 20),
    gg_reference: existing.ggReference,
    sa_reference: existing.saReference,
    occurence_date: accidentDate,
    occurence_time: getOptionalTime(formData, "occurenceTime", accidentDate),
    reported_date: reportedDate,
    claim_amount: getAmount(formData, "claimAmount", "Claim amount", existing.claimAmount),
    excess_amount: getAmount(formData, "excessAmount", "Excess amount", existing.excessAmount),
    call_refer: existing.callRefer,
    captured_person: getChoice(formData, "capturedPerson", "Capture person", ["?", "HM", "DF", "MDS", "CR", "JR", "MO", "AJ"]),
    fin_year: getChoice(formData, "finYear", "Financial year", ["02/03", "01/02", "00/01", "99/00", "98/99", "97/98"]),
    garage: getChoice(formData, "garage", "Garage", ["PTA", "JHB"]),
    driver_telno: getOptionalText(formData, "driverTelno", "Driver telephone", 30),
    driver_site_code: getOptionalInteger(formData, "driverSiteCode", "Site"),
    transoffic_name: getOptionalText(formData, "transportOfficerName", "Transport officer", 30),
    transoffic_tel: getOptionalText(formData, "transportOfficerTel", "Transport officer telephone", 20),
    accident_km: getAmount(formData, "accidentKm", "GG car km", existing.accidentKm),
    acc_type_code: getOptionalInteger(formData, "accidentTypeCode", "Accident category"),
    flag_gg_hq: flagGgHq,
    flag_gg_hq_date: flagGgHqDate,
    file_close_date: getOptionalDate(formData, "fileCloseDate", "File close date"),
    case_number: getRequiredText(formData, "caseNumber", "Case number", 15),
    reporting_authority: getOptionalText(formData, "reportingAuthority", "Authority", 60),
    cost_of_repair: getAmount(formData, "costOfRepair", "GG car damage", existing.costOfRepair),
    damage_description: getOptionalText(formData, "damageDescription", "GG damage description", 60),
    death: getChoice(formData, "death", "Death", ["?", "N", "Y"]),
    injured: getChoice(formData, "injured", "Injured", ["?", "N", "Y"]),
    third_party_regno: getOptionalText(formData, "thirdPartyRegistration", "Private party registration", 8),
    third_party_owner: getOptionalText(formData, "thirdPartyOwner", "Private party name", 30),
    third_party_tel: getOptionalText(formData, "thirdPartyTelephone", "Private party telephone", 30),
    third_party_claim: getAmount(formData, "thirdPartyClaim", "Private car damage", existing.thirdPartyClaim),
    SecondThirdPartyRegNo: getOptionalText(formData, "secondThirdPartyRegNo", "2nd third-party registration", 8),
    th_claim_receive: getChoice(formData, "claimReceived", "Claim received", ["N", "Y"]),
    claim_against_dept: getAmount(formData, "claimAmount", "Claim amount", existing.claimAgainstDepartment),
    letterhead: getChoice(formData, "letterhead", "Letterhead", ["N", "Y"]),
    z181: getChoice(formData, "z181", "Z181", ["N", "Y"]),
    part3: getChoice(formData, "part3", "Part III", ["N", "Y"]),
    statement: getChoice(formData, "statement", "Statement", ["N", "Y"]),
    sketch: getChoice(formData, "sketch", "Sketch", ["N", "Y"]),
    iddoc: getChoice(formData, "iddoc", "ID document", ["N", "Y"]),
    drivelic: getChoice(formData, "drivelic", "Driving licence", ["N", "Y"]),
    docs_acc_relieve: getChoice(formData, "documentsAccidentRelieve", "Documents received for relief", ["N", "Y"]),
    flag_case_num: getChoice(formData, "flagCaseNumber", "Case number received", ["N", "Y"]),
    trip_author: getChoice(formData, "tripAuthor", "Trip authority", ["N", "Y"]),
    flag_trip_author: flagTripAuthor,
    flag_trip_auth_date: flagTripAuthDate,
    driver_fault: getChoice(formData, "driverFault", "GG driver fault", ["Unknown", "Yes", "No", "Maybe"]),
    attorney_insure: getChoice(formData, "attorneyInsure", "Attorney / insurance", ["?", "ATT", "INS"]),
    insurance_claim: getChoice(formData, "insuranceClaim", "Claim against department", ["?", "Y", "N"]),
    priv_dampay_date: getOptionalDate(formData, "privateDamagePaymentDate", "Private damage payment date"),
    th_claim_accept_reject: getChoice(formData, "thirdPartyClaimDecision", "Claim accept/reject", ["?", "ACC", "REJ"]),
    th_claim_reject_reason: getOptionalText(formData, "thirdPartyClaimRejectReason", "Claim reject reason", 30),
    write_off_amount: getAmount(formData, "writeOffAmount", "Write-off amount", existing.writeOffAmount),
    write_off_date: getOptionalDate(formData, "writeOffDate", "Write-off date"),
    occurence_place: getOptionalText(formData, "occurencePlace", "Accident place", 50),
    tow_need: getChoice(formData, "towNeed", "Tow required", ["N", "Y"]),
    notes: getOptionalText(formData, "notes", "Notes", 60),
    date_created: existing.dateCreated,
    date_updated: existing.dateUpdated,
    created_by_user_code: existing.createdByUserCode,
    modified_by_user_code: existing.modifiedByUserCode,
    is_deleted: existing.isDeleted,
  };
}

function apiErrorMessage(error: AccidentApiError) {
  if (error.reason === "unauthorized") {
    return "Your session has expired or you are no longer allowed to edit accidents. Sign in again.";
  }

  if (error.reason === "not-found") {
    return "That accident record no longer exists.";
  }

  if (error.reason === "unavailable") {
    return "The accident service is temporarily unavailable. Please try again.";
  }

  return "The accident service rejected the submitted data. Check the fields and try again.";
}

export async function updateGarageAccidentAction(
  _previousState: GarageEditActionState = initialStatus,
  formData: FormData,
): Promise<GarageEditActionState> {
  const session = await getSession();
  if (session.status === "unavailable") {
    return { status: "error", message: "The sign-in service is temporarily unavailable. Please try again." };
  }

  if (session.status !== "authenticated") {
    return { status: "error", message: "Your session has expired. Sign in again before editing an accident." };
  }

  if (!hasRole(session.roles, ACCIDENTS_ROLE)) {
    return { status: "error", message: "You do not have permission to edit garage accidents." };
  }

  let accidentCode: number;
  try {
    accidentCode = getAccidentCode(formData);
  } catch (error) {
    return {
      status: "error",
      message: error instanceof AccidentFormValidationError ? error.message : "A valid accident record is required.",
    };
  }

  try {
    const existing = await getAccidentForEdit(accidentCode);
    await updateAccidentAgainstApi(buildUpdateRequest(formData, existing));
  } catch (error) {
    if (error instanceof AccidentFormValidationError) {
      return { status: "error", message: error.message };
    }

    if (error instanceof AccidentApiError) {
      return { status: "error", message: apiErrorMessage(error) };
    }

    console.error("FIS garage accident update failed", error instanceof Error ? error.message : "unknown error");
    return { status: "error", message: "Accident update failed. Please try again." };
  }

  revalidatePath("/accidents/garage");
  redirect("/accidents/garage?updated=1");
}

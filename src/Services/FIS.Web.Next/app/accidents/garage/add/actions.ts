"use server";

import { revalidatePath } from "next/cache";
import { redirect } from "next/navigation";

import {
  AccidentApiError,
  createAccidentAgainstApi,
  type CreateAccidentRequest,
} from "@/lib/api-accidents";
import { getSession } from "@/lib/session";

const ACCIDENTS_ROLE = "Accidents";

export type GarageAddActionState = {
  status: "idle" | "error";
  message?: string;
};

const initialStatus: GarageAddActionState = { status: "idle" };

class AccidentFormValidationError extends Error {}

function getText(formData: FormData, key: string) {
  const value = formData.get(key);
  return typeof value === "string" ? value.trim() : "";
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

  return value;
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

function getOptionalAmount(formData: FormData, key: string, label: string) {
  const value = getText(formData, key);
  if (!value) {
    return null;
  }

  const parsed = Number(value);
  if (!Number.isFinite(parsed) || parsed < 0) {
    throw new AccidentFormValidationError(`${label} must be a non-negative amount.`);
  }

  return parsed;
}

function getOptionalTime(formData: FormData, key: string) {
  const value = getText(formData, key);
  if (!value) {
    return null;
  }

  if (!/^(?:[01]\d|2[0-3]):[0-5]\d$/.test(value)) {
    throw new AccidentFormValidationError("Accident time must use the HH:MM format.");
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

function hasRole(roles: readonly string[], role: string) {
  return roles.some(
    (candidate) => candidate.localeCompare(role, undefined, { sensitivity: "accent" }) === 0,
  );
}

function validateAndBuildRequest(formData: FormData): CreateAccidentRequest {
  const vmfCode = getPositiveInteger(formData, "vmfCode", "Vehicle");
  const accidentDate = getDate(formData, "occurenceDate", "Accident date");
  const reportedDate = getDate(formData, "reportedDate", "Date reported");
  const accidentTime = getOptionalTime(formData, "occurenceTime");
  const driverEmployNumber = getOptionalText(
    formData,
    "driverEmployNumber",
    "Driver ID number",
    13,
  );

  if (driverEmployNumber && !/^[0-9/]+$/.test(driverEmployNumber)) {
    throw new AccidentFormValidationError("Driver ID number may contain only numbers and '/'.");
  }

  const description = getRequiredText(formData, "description", "Accident description", 60);
  const flagGgHq = getChoice(formData, "flagGgHq", "Notify HQ", ["N", "Y", "X"]);
  const flagGgHqDate = getOptionalDate(formData, "flagGgHqDate", "Notify HQ date");
  if ((flagGgHq === "Y" || flagGgHq === "X") && !flagGgHqDate) {
    throw new AccidentFormValidationError("Notify HQ date is required when Notify HQ is Y or X.");
  }

  const flagTripAuthor = getChoice(formData, "flagTripAuthor", "Notify trip authority", [
    "N",
    "Y",
    "X",
  ]);
  const flagTripAuthDate = getOptionalDate(formData, "flagTripAuthDate", "Notify trip date");
  if ((flagTripAuthor === "Y" || flagTripAuthor === "X") && !flagTripAuthDate) {
    throw new AccidentFormValidationError(
      "Notify trip date is required when Notify trip authority is Y or X.",
    );
  }

  return {
    vmf_code: vmfCode,
    description,
    driver_name: getOptionalText(formData, "driverName", "GG driver name", 25),
    driver_employ_number: driverEmployNumber,
    hq_reference: getOptionalText(formData, "hqReference", "HQ reference", 20),
    gg_reference: getOptionalText(formData, "ggReference", "GG reference", 20),
    sa_reference: null,
    occurence_date: `${accidentDate}T00:00:00.000Z`,
    occurence_time: accidentTime ? `${accidentDate}T${accidentTime}:00.000Z` : null,
    reported_date: `${reportedDate}T00:00:00.000Z`,
    claim_amount: getAmount(formData, "claimAmount", "Claim amount"),
    excess_amount: getAmount(formData, "excessAmount", "Excess amount"),
    call_refer: null,
    captured_person: getChoice(formData, "capturedPerson", "Capture person", [
      "?",
      "HM",
      "DF",
      "MDS",
      "CR",
      "JR",
      "MO",
      "AJ",
    ]),
    fin_year: getChoice(formData, "finYear", "Financial year", [
      "02/03",
      "01/02",
      "00/01",
      "99/00",
      "98/99",
      "97/98",
    ]),
    garage: getChoice(formData, "garage", "Garage", ["PTA", "JHB"]),
    driver_telno: getOptionalText(formData, "driverTelno", "Driver telephone", 30),
    driver_site_code: getOptionalInteger(formData, "driverSiteCode", "Site"),
    transoffic_name: getOptionalText(formData, "transportOfficerName", "Transport officer", 30),
    transoffic_tel: getOptionalText(
      formData,
      "transportOfficerTel",
      "Transport officer telephone",
      20,
    ),
    accident_km: getAmount(formData, "accidentKm", "GG car km"),
    acc_type_code: getOptionalInteger(formData, "accidentTypeCode", "Accident category"),
    flag_gg_hq: flagGgHq,
    flag_gg_hq_date: flagGgHqDate,
    file_close_date: getOptionalDate(formData, "fileCloseDate", "File close date"),
    case_number: getRequiredText(formData, "caseNumber", "Case number", 15),
    reporting_authority: getOptionalText(formData, "reportingAuthority", "Authority", 60),
    cost_of_repair: getAmount(formData, "costOfRepair", "GG car damage"),
    damage_description: getOptionalText(formData, "damageDescription", "GG damage description", 60),
    death: getChoice(formData, "death", "Death", ["?", "N", "Y"]),
    injured: getChoice(formData, "injured", "Injured", ["?", "N", "Y"]),
    third_party_regno: getOptionalText(
      formData,
      "thirdPartyRegistration",
      "Private party registration",
      8,
    ),
    third_party_owner: getOptionalText(formData, "thirdPartyOwner", "Private party name", 30),
    third_party_tel: getOptionalText(
      formData,
      "thirdPartyTelephone",
      "Private party telephone",
      30,
    ),
    third_party_claim: getAmount(formData, "thirdPartyClaim", "Private car damage"),
    SecondThirdPartyRegNo: getOptionalText(
      formData,
      "secondThirdPartyRegNo",
      "2nd third-party registration",
      8,
    ),
    th_claim_receive: getChoice(formData, "claimReceived", "Claim received", ["N", "Y"]),
    claim_against_dept: getAmount(formData, "claimAmount", "Claim amount"),
    letterhead: getChoice(formData, "letterhead", "Letterhead", ["N", "Y"]),
    z181: getChoice(formData, "z181", "Z181", ["N", "Y"]),
    part3: getChoice(formData, "part3", "Part III", ["N", "Y"]),
    statement: getChoice(formData, "statement", "Statement", ["N", "Y"]),
    sketch: getChoice(formData, "sketch", "Sketch", ["N", "Y"]),
    iddoc: getChoice(formData, "iddoc", "ID document", ["N", "Y"]),
    drivelic: getChoice(formData, "drivelic", "Driving licence", ["N", "Y"]),
    docs_acc_relieve: getChoice(
      formData,
      "documentsAccidentRelieve",
      "Documents received for relief",
      ["N", "Y"],
    ),
    flag_case_num: getChoice(formData, "flagCaseNumber", "Case number received", ["N", "Y"]),
    trip_author: getChoice(formData, "tripAuthor", "Trip authority", ["N", "Y"]),
    flag_trip_author: flagTripAuthor,
    flag_trip_auth_date: flagTripAuthDate,
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
    write_off_amount: getOptionalAmount(formData, "writeOffAmount", "Write-off amount"),
    write_off_date: getOptionalDate(formData, "writeOffDate", "Write-off date"),
    occurence_place: getOptionalText(formData, "occurencePlace", "Accident place", 50),
    tow_need: getChoice(formData, "towNeed", "Tow required", ["N", "Y"]),
    notes: getOptionalText(formData, "notes", "Notes", 60),
  };
}

export async function createGarageAccidentAction(
  _previousState: GarageAddActionState = initialStatus,
  formData: FormData,
): Promise<GarageAddActionState> {
  let request: CreateAccidentRequest;
  try {
    request = validateAndBuildRequest(formData);
  } catch (error) {
    if (error instanceof AccidentFormValidationError) {
      return { status: "error", message: error.message };
    }

    throw error;
  }

  const session = await getSession();
  if (session.status === "unavailable") {
    return {
      status: "error",
      message: "The sign-in service is temporarily unavailable. Please try again.",
    };
  }

  if (session.status !== "authenticated") {
    return {
      status: "error",
      message: "Your session has expired. Sign in again before adding an accident.",
    };
  }

  if (!hasRole(session.roles, ACCIDENTS_ROLE)) {
    return { status: "error", message: "You do not have permission to add garage accidents." };
  }

  try {
    await createAccidentAgainstApi(request);
  } catch (error) {
    if (error instanceof AccidentApiError) {
      return {
        status: "error",
        message:
          error.reason === "unauthorized"
            ? "Your session has expired or you are no longer allowed to add accidents. Sign in again."
            : error.reason === "unavailable"
              ? "The accident service is temporarily unavailable. Please try again."
              : "The accident service rejected the submitted data. Check the fields and try again.",
      };
    }

    console.error(
      "FIS garage accident creation failed",
      error instanceof Error ? error.message : "unknown error",
    );
    return { status: "error", message: "Accident creation failed. Please try again." };
  }

  revalidatePath("/accidents/garage");
  redirect("/accidents/garage?created=1");
}

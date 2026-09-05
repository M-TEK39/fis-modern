"use server";

import { revalidatePath } from "next/cache";
import { redirect } from "next/navigation";

import {
  updateCallCentreIncident,
  CallCentreApiError,
  type UpdateCallCentreRequest,
} from "@/lib/api-call-centre";
import { getSession } from "@/lib/session";

const EDIT_PATH = "/call-centre/incident/edit";
const CALL_CENTRE_ROLE = "Call Centre";

function getText(formData: FormData, ...keys: string[]) {
  for (const key of keys) {
    const value = formData.get(key);
    if (typeof value === "string") return value.trim();
  }

  return "";
}

function redirectWithError(message: string, referenceNumber = ""): never {
  const params = new URLSearchParams({ error: message });
  if (referenceNumber) params.set("referenceNumber", referenceNumber);
  redirect(`${EDIT_PATH}?${params.toString()}`);
}

async function authorizeCallCentre(referenceNumber: string) {
  const session = await getSession();
  if (session.status === "unavailable") {
    redirectWithError("The sign-in service is temporarily unavailable. Please try again.", referenceNumber);
  }

  if (session.status !== "authenticated") {
    redirectWithError("Your session has expired. Sign in again before continuing.", referenceNumber);
  }

  if (!session.roles.some((role) => role.localeCompare(CALL_CENTRE_ROLE, undefined, { sensitivity: "accent" }) === 0)) {
    redirectWithError("You do not have permission to update call centre incidents.", referenceNumber);
  }
}

function getRequiredCode(formData: FormData) {
  const rawCode = getText(formData, "cccode", "referenceNumber");
  const code = Number(rawCode);
  if (!rawCode || !Number.isInteger(code) || code <= 0 || code > 32_767) {
    throw new Error("Enter a valid GMT reference number.");
  }

  return { rawCode, code };
}

function getNullableNumber(formData: FormData, ...keys: string[]) {
  const value = getText(formData, ...keys);
  if (!value) return null;

  const parsed = Number(value);
  if (!Number.isInteger(parsed)) throw new Error("A numeric legacy value is invalid.");
  return parsed;
}

function getDate(formData: FormData, ...keys: string[]) {
  const value = getText(formData, ...keys);
  if (!value) return null;
  if (!/^\d{4}-\d{2}-\d{2}$/.test(value)) throw new Error("Dates must use yyyy-mm-dd format.");
  return value;
}

function getTime(formData: FormData, ...keys: string[]) {
  const value = getText(formData, ...keys);
  if (!value) return null;
  if (!/^\d{2}:\d{2}$/.test(value)) throw new Error("Times must use HH:mm format.");
  return `1970-01-01T${value}:00`;
}

function validateMaxLength(formData: FormData, field: string, maxLength: number, ...keys: string[]) {
  const value = getText(formData, ...keys);
  if (value.length > maxLength) throw new Error(`${field} must be ${maxLength} characters or fewer.`);
  return value || null;
}

function getRequest(formData: FormData): UpdateCallCentreRequest {
  const request: UpdateCallCentreRequest = {
    VmfCode: getNullableNumber(formData, "vmf_code", "vmfCode"),
    CallTime: getTime(formData, "Call_time", "callTime"),
    CallDate: getDate(formData, "Call_date", "callDate"),
    IncidentType: validateMaxLength(formData, "Incident type", 30, "Incident_type", "incidentType"),
    IncidentDesc: validateMaxLength(formData, "Incident description", 60, "Incident_Desc", "incidentDesc"),
    CaptureName: validateMaxLength(formData, "Capture name", 60, "Capture_name", "captureName"),
    UserAccessCode: getNullableNumber(formData, "User_access_code", "userAccessCode"),
    CallerName: validateMaxLength(formData, "Caller name", 30, "Caller_name", "callerName"),
    DriverName: validateMaxLength(formData, "Driver name", 30, "Driver_name", "driverName"),
    DriverPersalno: validateMaxLength(formData, "Driver Persal number", 15, "Driver_persalno", "driverPersalno"),
    DriverLicno: validateMaxLength(formData, "Driver licence number", 30, "Driver_Licno", "driverLicno"),
    GGNumber: validateMaxLength(formData, "GG number", 30, "GG_number", "ggNumber"),
    DriverBaseStation: validateMaxLength(formData, "Driver base station", 30, "Driver_base_station", "driverBaseStation"),
    DriverSite: getNullableNumber(formData, "Driver_Site", "driverSite"),
    DriverTel: validateMaxLength(formData, "Driver telephone", 30, "Driver_tel", "driverTel"),
    DriverCell: validateMaxLength(formData, "Driver cell", 30, "Driver_cell", "driverCell"),
    DriverFax: validateMaxLength(formData, "Driver fax", 30, "Driver_fax", "driverFax"),
    DriverEmail: validateMaxLength(formData, "Driver email", 240, "Driver_email", "driverEmail"),
    IncidentDate: getDate(formData, "Incident_date", "incidentDate"),
    IncidentTime: getTime(formData, "Incident_time", "incidentTime"),
    CallerTel: validateMaxLength(formData, "Caller telephone", 30, "Caller_tel", "callerTel"),
    TransportOfficerName: validateMaxLength(formData, "Transport officer name", 30, "TrOfficer_name", "transportOfficerName"),
    TransportOfficerTel: validateMaxLength(formData, "Transport officer telephone", 30, "TrOfficer_tel", "transportOfficerTel"),
    TransportOfficerSite: getNullableNumber(formData, "TrOfficer_Site", "transportOfficerSite"),
    IncidentTown: validateMaxLength(formData, "Incident town", 50, "Incident_town", "incidentTown"),
    IncidentStreet: validateMaxLength(formData, "Incident street", 30, "Incident_street", "incidentStreet"),
    Counter: getNullableNumber(formData, "Counter", "counter"),
    CallerFax: validateMaxLength(formData, "Caller fax", 30, "Caller_fax", "callerFax"),
    TransportOfficerFax: validateMaxLength(formData, "Transport officer fax", 30, "TrOfficer_fax", "transportOfficerFax"),
    CallerEmail: validateMaxLength(formData, "Caller email", 240, "Caller_email", "callerEmail"),
    TransportOfficerEmail: validateMaxLength(formData, "Transport officer email", 240, "TrOfficer_email", "transportOfficerEmail"),
    InformCro: validateMaxLength(formData, "CLO notification choice", 1, "Inform_CRO", "informCro"),
    CroRemarks: validateMaxLength(formData, "CLO remarks", 60, "CRO_Remarks", "croRemarks"),
    IncidentRemarks: validateMaxLength(formData, "Incident remarks", 80, "Incident_Remarks", "incidentRemarks"),
    NotifyListCode: getNullableNumber(formData, "Notify_list_code", "notifyListCode"),
    CallClosed: validateMaxLength(formData, "Call closed choice", 1, "call_closed", "callClosed"),
  };

  if (request.InformCro !== null && request.InformCro !== "Y" && request.InformCro !== "N") {
    throw new Error("CLO notification choice must be Y or N.");
  }
  if (request.CallClosed !== null && request.CallClosed !== "Y" && request.CallClosed !== "N") {
    throw new Error("Call closed choice must be Y or N.");
  }

  return request;
}

function apiErrorMessage(error: unknown) {
  if (error instanceof CallCentreApiError) {
    if (error.reason === "unauthorized") return "Your session has expired. Sign in again before continuing.";
    if (error.reason === "not-found") return "The call centre incident was not found.";
    if (error.reason === "unavailable") return "The call centre service is temporarily unavailable. Please try again.";
  }

  return "The call centre incident could not be updated. Please try again.";
}

export async function updateCallCentreIncidentAction(formData: FormData) {
  let referenceNumber = getText(formData, "cccode", "referenceNumber");
  try {
    const codeResult = getRequiredCode(formData);
    referenceNumber = codeResult.rawCode;
    await authorizeCallCentre(referenceNumber);
    await updateCallCentreIncident(codeResult.code, getRequest(formData));
  } catch (error) {
    if (error instanceof Error && !(error instanceof CallCentreApiError)) {
      redirectWithError(error.message, referenceNumber);
    }
    redirectWithError(apiErrorMessage(error), referenceNumber);
  }

  revalidatePath(EDIT_PATH);
  redirect(`${EDIT_PATH}?referenceNumber=${encodeURIComponent(referenceNumber)}&updated=1`);
}

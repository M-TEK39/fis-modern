"use server";

import { redirect } from "next/navigation";

import {
  CallCentreApiError,
  createCallCentreIncident,
} from "@/lib/api-call-centre";
import { getSession } from "@/lib/session";

const CAPTURE_PATH = "/call-centre/incident/capture";
const CALL_CENTRE_ROLE = "Call Centre";

function getText(formData: FormData, ...keys: string[]) {
  for (const key of keys) {
    const value = formData.get(key);
    if (typeof value === "string") {
      return value.trim();
    }
  }

  return "";
}

function redirectWithError(message: string, vmfCode = ""): never {
  const params = new URLSearchParams({ error: message });
  if (vmfCode) {
    params.set("vmfCode", vmfCode);
  }

  redirect(`${CAPTURE_PATH}?${params.toString()}`);
}

async function authorizeCallCentre(vmfCode: string) {
  const session = await getSession();
  if (session.status === "unavailable") {
    redirectWithError("The sign-in service is temporarily unavailable. Please try again.", vmfCode);
  }

  if (session.status !== "authenticated") {
    redirectWithError("Your session has expired. Sign in again before continuing.", vmfCode);
  }

  if (!session.roles.some((role) => role.localeCompare(CALL_CENTRE_ROLE, undefined, { sensitivity: "accent" }) === 0)) {
    redirectWithError("You do not have permission to capture call centre incidents.", vmfCode);
  }
}

function getPositiveInt(formData: FormData, ...keys: string[]) {
  const value = getText(formData, ...keys);
  const parsed = Number(value);
  return Number.isInteger(parsed) && parsed > 0 ? parsed : null;
}

function validateMaxLength(value: string, field: string, maxLength: number, vmfCode: string) {
  if (value.length > maxLength) {
    redirectWithError(`${field} must be ${maxLength} characters or fewer.`, vmfCode);
  }
}

function apiErrorMessage(error: unknown) {
  if (error instanceof CallCentreApiError) {
    if (error.reason === "unauthorized") {
      return "Your session has expired. Sign in again before continuing.";
    }

    if (error.reason === "not-found") {
      return "The selected vehicle was not found.";
    }

    if (error.reason === "unavailable") {
      return "The call centre service is temporarily unavailable. Please try again.";
    }
  }

  return "The incident could not be captured. Please try again.";
}

export async function saveQueryIncidentAction(formData: FormData) {
  const vmfCodeText = getText(formData, "ccVMF", "vmfCode");
  await authorizeCallCentre(vmfCodeText);

  const vmfCode = getPositiveInt(formData, "ccVMF", "vmfCode");
  if (vmfCode === null) {
    redirectWithError("A valid vehicle must be selected before capturing an incident.");
  }

  const incidentType = getText(formData, "xinctype", "incidentType") || "Query";
  if (incidentType !== "Query") {
    redirectWithError("This form only captures Query incidents.", vmfCodeText);
  }

  const informCro = getText(formData, "xcro", "informCro") || "N";
  if (informCro !== "Y" && informCro !== "N") {
    redirectWithError("The CLO notification choice is invalid.", vmfCodeText);
  }

  const croRemarks = getText(formData, "xcrem", "croRemarks");
  validateMaxLength(getText(formData, "xtrsname", "transportOfficerName"), "Transport officer name", 60, vmfCodeText);
  validateMaxLength(getText(formData, "xtrstel", "transportOfficerTel"), "Transport officer telephone", 15, vmfCodeText);
  validateMaxLength(getText(formData, "xtrsfax", "transportOfficerFax"), "Transport officer fax", 15, vmfCodeText);
  validateMaxLength(getText(formData, "xtrseml", "transportOfficerEmail"), "Transport officer email", 30, vmfCodeText);
  validateMaxLength(getText(formData, "xcalname", "callerName"), "Caller name", 30, vmfCodeText);
  validateMaxLength(getText(formData, "xcaltel", "callerTel"), "Caller telephone", 30, vmfCodeText);
  validateMaxLength(getText(formData, "xcalfax", "callerFax"), "Caller fax", 15, vmfCodeText);
  validateMaxLength(getText(formData, "xcaleml", "callerEmail"), "Caller email", 30, vmfCodeText);
  validateMaxLength(croRemarks, "CLO remarks", 60, vmfCodeText);
  validateMaxLength(getText(formData, "xirem", "incidentRemarks"), "Incident notes", 80, vmfCodeText);
  if (informCro === "Y" && !croRemarks) {
    redirectWithError("Remarks for the CLO are required when informing the CLO.", String(vmfCode));
  }

  const callerName = getText(formData, "xcalname", "callerName");
  const transportOfficerName = getText(formData, "xtrsname", "transportOfficerName");
  const callerTel = getText(formData, "xcaltel", "callerTel");
  const transportOfficerTel = getText(formData, "xtrstel", "transportOfficerTel");
  const callerFax = getText(formData, "xcalfax", "callerFax");
  const transportOfficerFax = getText(formData, "xtrsfax", "transportOfficerFax");
  const callerEmail = getText(formData, "xcaleml", "callerEmail");
  const transportOfficerEmail = getText(formData, "xtrseml", "transportOfficerEmail");
  const callClosed = getText(formData, "xclosed", "callClosed") || "Y";
  if (callClosed !== "Y" && callClosed !== "N") {
    redirectWithError("The call closed choice is invalid.", String(vmfCode));
  }

  try {
    const code = await createCallCentreIncident({
      VmfCode: vmfCode,
      IncidentType: incidentType,
      TransportOfficerName: transportOfficerName || null,
      TransportOfficerTel: transportOfficerTel || null,
      TransportOfficerFax: transportOfficerFax || null,
      TransportOfficerEmail: transportOfficerEmail || null,
      TransportOfficerSite: getPositiveInt(formData, "xtrssite", "transportOfficerSite"),
      CallerName: callerName || transportOfficerName || null,
      CallerTel: callerName ? callerTel || null : transportOfficerTel || null,
      CallerFax: callerName ? callerFax || null : transportOfficerFax || null,
      CallerEmail: callerName ? callerEmail || null : transportOfficerEmail || null,
      InformCro: informCro === "Y" ? "Y" : "N",
      CroRemarks: croRemarks || null,
      IncidentRemarks: getText(formData, "xirem", "incidentRemarks") || null,
      NotifyListCode: getPositiveInt(formData, "xnotel", "notifyListCode"),
      CallClosed: callClosed,
    });

    const params = new URLSearchParams({ saved: "1" });
    if (code !== null) {
      params.set("code", String(code));
    }
    redirect(`${CAPTURE_PATH}?${params.toString()}`);
  } catch (error) {
    redirectWithError(apiErrorMessage(error), String(vmfCode));
  }
}

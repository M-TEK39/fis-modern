"use server";

import { redirect } from "next/navigation";

import {
  CallCentreApiError,
  createCallCentreIncident,
  createRoadAssistanceIncident,
} from "@/lib/api-call-centre";
import { getSession } from "@/lib/session";

const CAPTURE_PATH = "/call-centre/incident/capture";
const ROAD_CAPTURE_PATH = "/CallCentre/MNT_road_getdata.aspx";
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

function redirectWithError(message: string, vmfCode = "", incidentType = "", path = CAPTURE_PATH): never {
  const params = new URLSearchParams({ error: message });
  if (vmfCode) {
    params.set("vmfCode", vmfCode);
  }
  if (incidentType) {
    params.set("incidentType", incidentType);
  }

  redirect(`${path}?${params.toString()}`);
}

function redirectRoadWithError(message: string, vmfCode = ""): never {
  const params = new URLSearchParams({ error: message, incidentType: "Road_Assistance" });
  if (vmfCode) {
    params.set("ccVMF", vmfCode);
  }

  redirect(`${ROAD_CAPTURE_PATH}?${params.toString()}`);
}

async function authorizeCallCentre(vmfCode: string, incidentType = "", path = CAPTURE_PATH) {
  const session = await getSession();
  if (session.status === "unavailable") {
    redirectWithError("The sign-in service is temporarily unavailable. Please try again.", vmfCode, incidentType, path);
  }

  if (session.status !== "authenticated") {
    redirectWithError("Your session has expired. Sign in again before continuing.", vmfCode, incidentType, path);
  }

  if (!session.roles.some((role) => role.localeCompare(CALL_CENTRE_ROLE, undefined, { sensitivity: "accent" }) === 0)) {
    redirectWithError("You do not have permission to capture call centre incidents.", vmfCode, incidentType, path);
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

function validateRoadMaxLength(value: string, field: string, maxLength: number, vmfCode: string) {
  if (value.length > maxLength) {
    redirectRoadWithError(`${field} must be ${maxLength} characters or fewer.`, vmfCode);
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

export async function saveRoadAssistanceAction(formData: FormData) {
  const vmfCodeText = getText(formData, "ccVMF", "vmfCode");
  await authorizeCallCentre(vmfCodeText, "Road_Assistance", ROAD_CAPTURE_PATH);

  const vmfCode = getPositiveInt(formData, "ccVMF", "vmfCode");
  if (vmfCode === null) {
    redirectRoadWithError("A valid vehicle must be selected before capturing an incident.", vmfCodeText);
  }

  const incidentType = getText(formData, "xinctype", "incidentType") || "Road_Assistance";
  if (incidentType !== "Road_Assistance") {
    redirectRoadWithError("This form only captures Road Assistance incidents.", vmfCodeText);
  }

  const incidentDate = getText(formData, "xincdat", "incidentDate");
  const incidentTime = getText(formData, "xinctime", "incidentTime");
  if (!/^\d{4}-\d{2}-\d{2}$/.test(incidentDate)) {
    redirectRoadWithError("Enter a valid incident date.", vmfCodeText);
  }
  if (!/^\d{2}:\d{2}$/.test(incidentTime)) {
    redirectRoadWithError("Enter the incident time in HH:mm format.", vmfCodeText);
  }

  const informCro = getText(formData, "xcro", "informCro") || "N";
  if (informCro !== "Y" && informCro !== "N") {
    redirectRoadWithError("The CLO notification choice is invalid.", vmfCodeText);
  }

  const callClosed = getText(formData, "xclosed", "callClosed") || "N";
  if (callClosed !== "Y" && callClosed !== "N") {
    redirectRoadWithError("The call closed choice is invalid.", vmfCodeText);
  }

  const transportOfficerName = getText(formData, "xtrsname", "transportOfficerName");
  const transportOfficerTel = getText(formData, "xtrstel", "transportOfficerTel");
  const transportOfficerFax = getText(formData, "xtrsfax", "transportOfficerFax");
  const transportOfficerEmail = getText(formData, "xtrseml", "transportOfficerEmail");
  const callerName = getText(formData, "xcalname", "callerName");
  const callerTel = getText(formData, "xcaltel", "callerTel");
  const callerFax = getText(formData, "xcalfax", "callerFax");
  const callerEmail = getText(formData, "xcaleml", "callerEmail");
  const driverName = getText(formData, "xdrvname", "driverName");
  const driverTel = getText(formData, "xdrvtel", "driverTel");
  const driverPersalno = getText(formData, "xdrvperno", "driverPersalno");
  const croRemarks = getText(formData, "xcrem", "croRemarks");
  const town = getText(formData, "x2town", "town");
  const suburb = getText(formData, "x1town", "suburb");
  const street = getText(formData, "xstreet", "street");
  const vehicleProblem = getText(formData, "xincdesc", "vehicleProblem");
  const towingRemarks = getText(formData, "xrem", "towingRemarks");
  const location = [suburb, town].filter(Boolean).join(" ; ");

  validateRoadMaxLength(transportOfficerName, "Transport officer name", 60, vmfCodeText);
  validateRoadMaxLength(transportOfficerTel, "Transport officer telephone", 15, vmfCodeText);
  validateRoadMaxLength(transportOfficerFax, "Transport officer fax", 15, vmfCodeText);
  validateRoadMaxLength(transportOfficerEmail, "Transport officer email", 30, vmfCodeText);
  validateRoadMaxLength(callerName, "Caller name", 30, vmfCodeText);
  validateRoadMaxLength(callerTel, "Caller telephone", 30, vmfCodeText);
  validateRoadMaxLength(callerFax, "Caller fax", 15, vmfCodeText);
  validateRoadMaxLength(callerEmail, "Caller email", 30, vmfCodeText);
  validateRoadMaxLength(driverName, "Driver name", 60, vmfCodeText);
  validateRoadMaxLength(driverTel, "Driver telephone", 30, vmfCodeText);
  validateRoadMaxLength(driverPersalno, "Driver personnel number", 15, vmfCodeText);
  validateRoadMaxLength(croRemarks, "CLO remarks", 60, vmfCodeText);
  validateRoadMaxLength(town, "Town", 20, vmfCodeText);
  validateRoadMaxLength(suburb, "Suburb", 30, vmfCodeText);
  validateRoadMaxLength(street, "Street name", 30, vmfCodeText);
  validateRoadMaxLength(vehicleProblem, "Vehicle problem", 60, vmfCodeText);
  validateRoadMaxLength(towingRemarks, "Towing remarks", 50, vmfCodeText);
  validateRoadMaxLength(location, "Location", 50, vmfCodeText);
  if (informCro === "Y" && !croRemarks) {
    redirectRoadWithError("Remarks for the CLO are required when informing the CLO.", String(vmfCode));
  }

  let result: Awaited<ReturnType<typeof createRoadAssistanceIncident>>;
  try {
    result = await createRoadAssistanceIncident({
      VmfCode: vmfCode,
      IncidentType: "Road_Assistance",
      GGNumber: getText(formData, "xgg", "ggNumber") || null,
      IncidentDate: `${incidentDate}T00:00:00`,
      IncidentTime: `${incidentDate}T${incidentTime}:00`,
      IncidentTown: location || null,
      IncidentStreet: street || null,
      DriverName: driverName || transportOfficerName || null,
      DriverTel: driverName ? driverTel || null : transportOfficerTel || null,
      DriverPersalno: driverPersalno || null,
      TransportOfficerName: transportOfficerName || null,
      TransportOfficerTel: transportOfficerTel || null,
      TransportOfficerFax: transportOfficerFax || null,
      TransportOfficerEmail: transportOfficerEmail || null,
      TransportOfficerSite: getPositiveInt(formData, "xtrssite", "transportOfficerSite"),
      CallerName: callerName || transportOfficerName || null,
      CallerTel: callerName ? callerTel || null : transportOfficerTel || null,
      CallerFax: callerName ? callerFax || null : transportOfficerFax || null,
      CallerEmail: callerName ? callerEmail || null : transportOfficerEmail || null,
      InformCro: informCro,
      CroRemarks: croRemarks || null,
      IncidentRemarks: null,
      NotifyListCode: getPositiveInt(formData, "xnotc", "notifyListCode"),
      CallClosed: callClosed,
      TowingLocationStart: location || null,
      VehicleProblem: vehicleProblem || null,
      TowingRemarks: towingRemarks || null,
      TowTruckCode: getPositiveInt(formData, "xtruckcod", "towTruckCode"),
    });

  } catch (error) {
    redirectRoadWithError(apiErrorMessage(error), String(vmfCode));
  }

  const params = new URLSearchParams({
    saved: "1",
    incidentType: "Road_Assistance",
    ccVMF: String(vmfCode),
    code: String(result.callCentreCode),
  });
  redirect(`${ROAD_CAPTURE_PATH}?${params.toString()}`);
}

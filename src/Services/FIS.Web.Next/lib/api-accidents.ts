import "server-only";

import { getForwardedAuthCookieHeader } from "@/lib/api-auth";

const API_TIMEOUT_MS = 8_000;

type JsonRecord = Record<string, unknown>;

export type GarageSearchType = "GG" | "GP";

export type GarageAccidentRow = {
  accidentCode: number;
  vehicleNumber: string | null;
  hireType: string | null;
  accidentDate: string | null;
  reference: string | null;
};

export type GarageAccidentPage = {
  rows: GarageAccidentRow[];
  page: number;
  pageSize: number;
  totalRecords: number;
  totalPages: number;
  searchTerm: string;
  searchType: GarageSearchType;
};

export type AccidentVehicleOption = {
  vmfCode: number;
  fleetNumber: string | null;
  registrationNumber: string | null;
};

export type AccidentSiteOption = {
  siteCode: number;
  departmentNumber: string | null;
  description: string | null;
};

export type AccidentTypeOption = {
  typeCode: number;
  description: string;
};

type AccidentLegacyWriteFields = {
  call_refer: number | null;
  captured_person: string | null;
  fin_year: string | null;
  garage: string | null;
  driver_telno: string | null;
  driver_site_code: number | null;
  transoffic_name: string | null;
  transoffic_tel: string | null;
  accident_km: number | null;
  acc_type_code: number | null;
  flag_gg_hq: string | null;
  flag_gg_hq_date: string | null;
  file_close_date: string | null;
  case_number: string | null;
  reporting_authority: string | null;
  cost_of_repair: number | null;
  damage_description: string | null;
  death: string | null;
  injured: string | null;
  third_party_regno: string | null;
  third_party_owner: string | null;
  third_party_tel: string | null;
  third_party_claim: number | null;
  SecondThirdPartyRegNo: string | null;
  th_claim_receive: string | null;
  claim_against_dept: number | null;
  letterhead: string | null;
  z181: string | null;
  part3: string | null;
  statement: string | null;
  sketch: string | null;
  iddoc: string | null;
  drivelic: string | null;
  docs_acc_relieve: string | null;
  flag_case_num: string | null;
  trip_author: string | null;
  flag_trip_author: string | null;
  flag_trip_auth_date: string | null;
  driver_fault: string | null;
  attorney_insure: string | null;
  insurance_claim: string | null;
  priv_dampay_date: string | null;
  th_claim_accept_reject: string | null;
  th_claim_reject_reason: string | null;
  write_off_amount: number | null;
  write_off_date: string | null;
  occurence_place: string | null;
  tow_need: string | null;
  notes: string | null;
};

export type CreateAccidentRequest = AccidentLegacyWriteFields & {
  vmf_code: number;
  description: string;
  driver_name: string | null;
  driver_employ_number: string | null;
  hq_reference: string | null;
  gg_reference: string | null;
  sa_reference: string | null;
  occurence_date: string;
  occurence_time: string | null;
  reported_date: string;
  claim_amount: number;
  excess_amount: number;
};

export type AccidentEditRecord = {
  accidentCode: number;
  vmfCode: number;
  postingMonthCode: number | null;
  description: string | null;
  driverName: string | null;
  driverEmployNumber: string | null;
  hqReference: string | null;
  ggReference: string | null;
  saReference: string | null;
  occurenceDate: string | null;
  occurenceTime: string | null;
  reportedDate: string | null;
  claimAmount: number | null;
  excessAmount: number | null;
  dateCreated: string;
  dateUpdated: string | null;
  createdByUserCode: number | null;
  modifiedByUserCode: number | null;
  isDeleted: boolean;
  callRefer: number | null;
  capturedPerson: string | null;
  finYear: string | null;
  garage: string | null;
  driverTelno: string | null;
  driverSiteCode: number | null;
  transportOfficerName: string | null;
  transportOfficerTel: string | null;
  accidentKm: number | null;
  accidentTypeCode: number | null;
  flagGgHq: string | null;
  flagGgHqDate: string | null;
  fileCloseDate: string | null;
  caseNumber: string | null;
  reportingAuthority: string | null;
  costOfRepair: number | null;
  damageDescription: string | null;
  death: string | null;
  injured: string | null;
  thirdPartyRegistration: string | null;
  thirdPartyOwner: string | null;
  thirdPartyTelephone: string | null;
  thirdPartyClaim: number | null;
  secondThirdPartyRegNo: string | null;
  claimReceived: string | null;
  claimAgainstDepartment: number | null;
  letterhead: string | null;
  z181: string | null;
  part3: string | null;
  statement: string | null;
  sketch: string | null;
  iddoc: string | null;
  drivelic: string | null;
  documentsAccidentRelieve: string | null;
  flagCaseNumber: string | null;
  tripAuthor: string | null;
  flagTripAuthor: string | null;
  flagTripAuthDate: string | null;
  driverFault: string | null;
  attorneyInsure: string | null;
  insuranceClaim: string | null;
  privateDamagePaymentDate: string | null;
  thirdPartyClaimDecision: string | null;
  thirdPartyClaimRejectReason: string | null;
  writeOffAmount: number | null;
  writeOffDate: string | null;
  occurencePlace: string | null;
  towNeed: string | null;
  notes: string | null;
  vehicleFleetNumber: string | null;
  vehicleRegistrationNumber: string | null;
};

export type AccidentUpdateRequest = AccidentLegacyWriteFields & {
  accident_code: number;
  vmf_code: number;
  posting_month_code: number | null;
  description: string | null;
  driver_name: string | null;
  driver_employ_number: string | null;
  hq_reference: string | null;
  gg_reference: string | null;
  sa_reference: string | null;
  occurence_date: string | null;
  occurence_time: string | null;
  reported_date: string | null;
  claim_amount: number | null;
  excess_amount: number | null;
  date_created: string;
  date_updated: string | null;
  created_by_user_code: number | null;
  modified_by_user_code: number | null;
  is_deleted: boolean;
};

export type AccidentApiErrorReason = "unauthorized" | "unavailable" | "invalid-response" | "not-found";

export type AccidentDriverReportMode = "name" | "id";

export type AccidentDriverReportRow = {
  registrationNumber: string | null;
  fleetNumber: string | null;
  driverName: string | null;
  driverEmployNumber: string | null;
  accidentDate: string | null;
  departmentNumber: string | null;
  siteDescription: string | null;
  costOfRepair: number | null;
};

export type AccidentVehicleReportMode = "registration" | "fleet";

export type AccidentPrivateVehicleReportMode = "third-party" | "description";

export type AccidentNewAccidentReportMode = "all" | "call" | "garage" | "confirm";

export type AccidentAllReportDateMode = "2002-current" | "1999-2001" | "before-1999";

export type AccidentGarageReportMode = "jhb" | "pta" | "all";

export type AccidentDepartmentPeriodVipMode = "all" | "vip" | "pool" | "permanent";

export type AccidentPeriodReportStatus = "open" | "closed";

export type AccidentVehicleReportRow = {
  accidentCode: number;
  registrationNumber: string | null;
  fleetNumber: string | null;
  locationDescription: string | null;
  accidentDate: string | null;
  accidentTime: string | null;
  accidentPlace: string | null;
  financialYear: string | null;
  callRefer: number | null;
  hireType: string | null;
  dateUpdated: string | null;
  notifiedGarage: string | null;
  notifiedGarageDate: string | null;
  notifiedTripAuthority: string | null;
  notifiedTripAuthorityDate: string | null;
  description: string | null;
  accidentTypeDescription: string | null;
  tripAuthority: string | null;
  driverName: string | null;
  driverEmployNumber: string | null;
  departmentNumber: string | null;
  siteDescription: string | null;
  transportOfficerName: string | null;
  transportOfficerTelephone: string | null;
  hqReference: string | null;
  ggReference: string | null;
  saReference: string | null;
  caseNumber: string | null;
  costOfRepair: number | null;
  damageDescription: string | null;
  driverFault: string | null;
  death: string | null;
  injured: string | null;
  thirdPartyRegistration: string | null;
  thirdPartyOwner: string | null;
  thirdPartyClaim: number | null;
  privateDamagePaymentDate: string | null;
  insuranceClaim: string | null;
  claimReceived: string | null;
  claimAgainstDepartment: number | null;
  claimDecision: string | null;
  claimRejectReason: string | null;
  writeOffAmount: number | null;
  writeOffDate: string | null;
  letterhead: string | null;
  z181: string | null;
  fileCloseDate: string | null;
  notes: string | null;
};

export type AccidentPeriodReportRow = {
  registrationNumber: string | null;
  fleetNumber: string | null;
  accidentDate: string | null;
  departmentNumber: string | null;
  siteDescription: string | null;
  hireType: string | null;
  accidentDescription: string | null;
  driverName: string | null;
  transportOfficerName: string | null;
  callRefer: number | null;
  costOfRepair: number | null;
  fileCloseDate: string | null;
};

export class AccidentApiError extends Error {
  constructor(
    public readonly reason: AccidentApiErrorReason,
    message: string,
  ) {
    super(message);
    this.name = "AccidentApiError";
  }
}

function getApiBaseUrl() {
  const value = process.env.API_BASE_URL?.trim() || "http://localhost:5010";
  return `${value.replace(/\/$/, "")}/`;
}

function isRecord(value: unknown): value is JsonRecord {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

function getValue(record: JsonRecord, ...keys: string[]) {
  for (const key of keys) {
    if (key in record) {
      return record[key];
    }
  }

  return undefined;
}

function asString(value: unknown) {
  if (typeof value === "string") {
    return value.trim() || null;
  }

  if (typeof value === "number" || typeof value === "bigint") {
    return String(value);
  }

  return null;
}

function asNumber(value: unknown) {
  if (typeof value === "number" && Number.isFinite(value)) {
    return value;
  }

  if (typeof value === "string" && value.trim()) {
    const parsed = Number(value);
    return Number.isFinite(parsed) ? parsed : null;
  }

  return null;
}

function getCollection(payload: unknown) {
  if (Array.isArray(payload)) {
    return payload;
  }

  if (isRecord(payload)) {
    const collection = getValue(payload, "data", "items", "results");
    return Array.isArray(collection) ? collection : [];
  }

  return [];
}

function mapPresent<T>(values: readonly unknown[], mapper: (value: unknown) => T | null) {
  const result: T[] = [];
  for (const value of values) {
    const mapped = mapper(value);
    if (mapped !== null) {
      result.push(mapped);
    }
  }
  return result;
}

async function requestApi(path: string, init: RequestInit = {}) {
  const cookieHeader = await getForwardedAuthCookieHeader();
  if (!cookieHeader) {
    throw new AccidentApiError("unauthorized", "No FIS access cookie is available.");
  }

  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), API_TIMEOUT_MS);

  try {
    const response = await fetch(new URL(path.replace(/^\//, ""), getApiBaseUrl()), {
      ...init,
      cache: "no-store",
      headers: {
        accept: "application/json",
        cookie: cookieHeader,
        ...(init.body ? { "content-type": "application/json" } : {}),
        ...init.headers,
      },
      signal: controller.signal,
    });

    if (response.status === 401 || response.status === 403) {
      throw new AccidentApiError("unauthorized", "The FIS access cookie was rejected.");
    }

    if (response.status === 404) {
      throw new AccidentApiError("not-found", "The accident record was not found.");
    }

    if (!response.ok) {
      throw new AccidentApiError(
        response.status >= 500 ? "unavailable" : "invalid-response",
        `FIS API returned HTTP ${response.status}.`,
      );
    }

    if (response.status === 204) {
      return null;
    }

    try {
      return (await response.json()) as unknown;
    } catch {
      throw new AccidentApiError("invalid-response", "The FIS API returned invalid JSON.");
    }
  } catch (error) {
    if (error instanceof AccidentApiError) {
      throw error;
    }

    throw new AccidentApiError("unavailable", "The FIS API could not be reached.");
  } finally {
    clearTimeout(timeout);
  }
}

type VehicleLookup = {
  vmfCode: number;
  fleetNumber: string | null;
  registrationNumber: string | null;
  typeCode: number | null;
};

function mapVehicle(value: unknown): VehicleLookup | null {
  if (!isRecord(value)) {
    return null;
  }

  const vmfCode = asNumber(getValue(value, "vmf_code", "vmfCode"));
  if (vmfCode === null) {
    return null;
  }

  return {
    vmfCode,
    fleetNumber: asString(getValue(value, "fleet_number", "fleetNumber")),
    registrationNumber: asString(getValue(value, "registration_number", "registrationNumber")),
    typeCode: asNumber(getValue(value, "type_code", "typeCode")),
  };
}

export async function getAccidentVehicleOptions(searchType: GarageSearchType, searchTerm = "") {
  const normalizedSearchTerm = searchTerm.trim();
  const path = normalizedSearchTerm
    ? `api/vehicles/search?searchTerm=${encodeURIComponent(normalizedSearchTerm)}`
    : "api/vehicles";
  const vehicles = mapPresent(getCollection(await requestApi(path)), mapVehicle);

  const normalizedLowerTerm = normalizedSearchTerm.toLowerCase();
  return vehicles
    .filter((vehicle) => {
      if (!normalizedLowerTerm) {
        return true;
      }

      const value = searchType === "GG" ? vehicle.fleetNumber : vehicle.registrationNumber;
      return (value?.trim().toLowerCase() ?? "") === normalizedLowerTerm;
    })
    .map((vehicle) => ({
      vmfCode: vehicle.vmfCode,
      fleetNumber: vehicle.fleetNumber,
      registrationNumber: vehicle.registrationNumber,
    }) satisfies AccidentVehicleOption)
    .toSorted((left, right) => {
      const leftLabel = left.fleetNumber ?? left.registrationNumber ?? String(left.vmfCode);
      const rightLabel = right.fleetNumber ?? right.registrationNumber ?? String(right.vmfCode);
      return leftLabel.localeCompare(rightLabel);
    });
}

function mapAccidentEditRecord(value: unknown): AccidentEditRecord {
  if (!isRecord(value)) {
    throw new AccidentApiError("invalid-response", "The FIS API returned an invalid accident record.");
  }

  const accidentCode = asNumber(getValue(value, "accident_code", "accidentCode"));
  const vmfCode = asNumber(getValue(value, "vmf_code", "vmfCode"));
  const dateCreated =
    asString(getValue(value, "date_created", "dateCreated")) ??
    asString(getValue(value, "date_updated", "dateUpdated")) ??
    asString(getValue(value, "reported_date", "reportedDate")) ??
    asString(getValue(value, "occurence_date", "occurrence_date", "occurenceDate"));
  if (accidentCode === null || vmfCode === null || !dateCreated) {
    throw new AccidentApiError("invalid-response", "The FIS API returned an incomplete accident record.");
  }

  const vehicle = getValue(value, "vehicle", "Vehicle");
  const vehicleRecord = isRecord(vehicle) ? vehicle : null;

  return {
    accidentCode,
    vmfCode,
    postingMonthCode: asNumber(getValue(value, "posting_month_code", "postingMonthCode")),
    description: asString(getValue(value, "description")),
    driverName: asString(getValue(value, "driver_name", "driverName")),
    driverEmployNumber: asString(getValue(value, "driver_employ_number", "driverEmployNumber")),
    hqReference: asString(getValue(value, "hq_reference", "hqReference")),
    ggReference: asString(getValue(value, "gg_reference", "ggReference")),
    saReference: asString(getValue(value, "sa_reference", "saReference")),
    occurenceDate: asString(getValue(value, "occurence_date", "occurrence_date", "occurenceDate")),
    occurenceTime: asString(getValue(value, "occurence_time", "occurenceTime")),
    reportedDate: asString(getValue(value, "reported_date", "reportedDate")),
    claimAmount: asNumber(getValue(value, "claim_amount", "claimAmount")),
    excessAmount: asNumber(getValue(value, "excess_amount", "excessAmount")),
    dateCreated,
    dateUpdated: asString(getValue(value, "date_updated", "dateUpdated")),
    createdByUserCode: asNumber(getValue(value, "created_by_user_code", "createdByUserCode")),
    modifiedByUserCode: asNumber(getValue(value, "modified_by_user_code", "modifiedByUserCode")),
    isDeleted: getValue(value, "is_deleted", "isDeleted") === true,
    callRefer: asNumber(getValue(value, "Call_Refer", "call_Refer", "callRefer")),
    capturedPerson: asString(getValue(value, "captured_person", "capturedPerson")),
    finYear: asString(getValue(value, "fin_year", "finYear")),
    garage: asString(getValue(value, "garage")),
    driverTelno: asString(getValue(value, "driver_telno", "driverTelno")),
    driverSiteCode: asNumber(getValue(value, "driver_site_code", "driverSiteCode")),
    transportOfficerName: asString(getValue(value, "transoffic_name", "transofficName")),
    transportOfficerTel: asString(getValue(value, "transoffic_tel", "transofficTel")),
    accidentKm: asNumber(getValue(value, "accident_km", "accidentKm")),
    accidentTypeCode: asNumber(getValue(value, "acc_type_code", "accTypeCode")),
    flagGgHq: asString(getValue(value, "Flag_gg_hq", "flag_gg_hq", "flagGgHq")),
    flagGgHqDate: asString(getValue(value, "Flag_gg_hq_date", "flag_gg_hq_date", "flagGgHqDate")),
    fileCloseDate: asString(getValue(value, "file_close_date", "fileCloseDate")),
    caseNumber: asString(getValue(value, "case_number", "caseNumber")),
    reportingAuthority: asString(getValue(value, "reporting_authority", "reportingAuthority")),
    costOfRepair: asNumber(getValue(value, "cost_of_repair", "costOfRepair")),
    damageDescription: asString(getValue(value, "damage_description", "damageDescription")),
    death: asString(getValue(value, "death")),
    injured: asString(getValue(value, "injured")),
    thirdPartyRegistration: asString(getValue(value, "third_party_regno", "thirdPartyRegno")),
    thirdPartyOwner: asString(getValue(value, "third_party_owner", "thirdPartyOwner")),
    thirdPartyTelephone: asString(getValue(value, "third_party_tel", "thirdPartyTel")),
    thirdPartyClaim: asNumber(getValue(value, "third_party_claim", "thirdPartyClaim")),
    secondThirdPartyRegNo: asString(getValue(value, "SecondThirdPartyRegNo", "secondThirdPartyRegNo")),
    claimReceived: asString(getValue(value, "th_claim_receive", "claimReceived")),
    claimAgainstDepartment:
      asNumber(getValue(value, "claim_amount", "claimAmount")) ??
      asNumber(getValue(value, "claim_against_dept", "claimAgainstDepartment")),
    letterhead: asString(getValue(value, "letterhead")),
    z181: asString(getValue(value, "z181", "Z181")),
    part3: asString(getValue(value, "part3")),
    statement: asString(getValue(value, "statement")),
    sketch: asString(getValue(value, "sketch")),
    iddoc: asString(getValue(value, "iddoc")),
    drivelic: asString(getValue(value, "drivelic")),
    documentsAccidentRelieve: asString(getValue(value, "docs_acc_relieve", "documentsAccidentRelieve")),
    flagCaseNumber: asString(getValue(value, "flag_case_num", "flagCaseNumber")),
    tripAuthor: asString(getValue(value, "trip_author", "tripAuthor")),
    flagTripAuthor: asString(getValue(value, "Flag_trip_author", "flag_trip_author", "flagTripAuthor")),
    flagTripAuthDate: asString(getValue(value, "flag_trip_auth_date", "flagTripAuthDate")),
    driverFault: asString(getValue(value, "driver_fault", "driverFault")),
    attorneyInsure: asString(getValue(value, "attorney_insure", "attorneyInsure")),
    insuranceClaim: asString(getValue(value, "insurance_claim", "insuranceClaim")),
    privateDamagePaymentDate: asString(getValue(value, "priv_dampay_date", "privateDamagePaymentDate")),
    thirdPartyClaimDecision: asString(getValue(value, "th_claim_accept_reject", "thirdPartyClaimDecision")),
    thirdPartyClaimRejectReason: asString(getValue(value, "th_claim_reject_reason", "thirdPartyClaimRejectReason")),
    writeOffAmount: asNumber(getValue(value, "write_off_amount", "writeOffAmount")),
    writeOffDate: asString(getValue(value, "write_off_date", "writeOffDate")),
    occurencePlace: asString(getValue(value, "occurence_place", "occurrence_place", "occurencePlace")),
    towNeed: asString(getValue(value, "Tow_need", "tow_need", "towNeed")),
    notes: asString(getValue(value, "notes")),
    vehicleFleetNumber: vehicleRecord
      ? asString(getValue(vehicleRecord, "fleet_number", "fleetNumber"))
      : null,
    vehicleRegistrationNumber: vehicleRecord
      ? asString(getValue(vehicleRecord, "registration_number", "registrationNumber"))
      : null,
  };
}

export async function getAccidentForEdit(accidentCode: number) {
  return mapAccidentEditRecord(await requestApi(`api/accidents/${encodeURIComponent(accidentCode)}`));
}

function mapAccidentDriverReportRow(value: unknown): AccidentDriverReportRow | null {
  if (!isRecord(value)) {
    return null;
  }

  return {
    registrationNumber: asString(getValue(value, "registration_number", "registrationNumber")),
    fleetNumber: asString(getValue(value, "fleet_number", "fleetNumber")),
    driverName: asString(getValue(value, "driver_name", "driverName")),
    driverEmployNumber: asString(getValue(value, "driver_employ_number", "driverEmployNumber")),
    accidentDate: asString(getValue(value, "occurence_date", "occurrence_date", "occurenceDate")),
    departmentNumber: asString(getValue(value, "department_number", "departmentNumber")),
    siteDescription: asString(getValue(value, "site_description", "siteDescription")),
    costOfRepair: asNumber(getValue(value, "cost_of_repair", "costOfRepair")),
  };
}

export async function getAccidentDriverReport(
  searchTerm: string,
  mode: AccidentDriverReportMode,
) {
  const query = new URLSearchParams({
    searchTerm: searchTerm.trim(),
    mode,
  });
  return mapPresent(getCollection(await requestApi(`api/accidents/reports/driver?${query.toString()}`)), mapAccidentDriverReportRow);
}

function mapAccidentVehicleReportRow(value: unknown): AccidentVehicleReportRow | null {
  if (!isRecord(value)) {
    return null;
  }

  const accidentCode = asNumber(getValue(value, "accident_code", "accidentCode"));
  if (accidentCode === null) {
    return null;
  }

  return {
    accidentCode,
    registrationNumber: asString(getValue(value, "registration_number", "registrationNumber")),
    fleetNumber: asString(getValue(value, "fleet_number", "fleetNumber")),
    locationDescription: asString(getValue(value, "location_description", "locationDescription")),
    accidentDate: asString(getValue(value, "occurence_date", "occurrence_date", "occurenceDate")),
    accidentTime: asString(getValue(value, "occurence_time", "occurrence_time", "occurenceTime")),
    accidentPlace: asString(getValue(value, "occurence_place", "occurrence_place", "occurencePlace")),
    financialYear: asString(getValue(value, "fin_year", "financialYear")),
    callRefer: asNumber(getValue(value, "Call_Refer", "call_Refer", "call_refer", "callRefer")),
    hireType: asString(getValue(value, "hire_type", "hireType")),
    dateUpdated: asString(getValue(value, "date_updated", "dateUpdated")),
    notifiedGarage: asString(getValue(value, "Flag_gg_hq", "flag_gg_hq", "notifiedGarage")),
    notifiedGarageDate: asString(getValue(value, "Flag_gg_hq_date", "flag_gg_hq_date", "notifiedGarageDate")),
    notifiedTripAuthority: asString(getValue(value, "Flag_trip_author", "flag_trip_author", "notifiedTripAuthority")),
    notifiedTripAuthorityDate: asString(getValue(value, "flag_trip_auth_date", "Flag_trip_auth_date", "notifiedTripAuthorityDate")),
    description: asString(getValue(value, "description")),
    accidentTypeDescription: asString(getValue(value, "accident_type_description", "accidentTypeDescription")),
    tripAuthority: asString(getValue(value, "trip_author", "tripAuthority")),
    driverName: asString(getValue(value, "driver_name", "driverName")),
    driverEmployNumber: asString(getValue(value, "driver_employ_number", "driverEmployNumber")),
    departmentNumber: asString(getValue(value, "department_number", "departmentNumber")),
    siteDescription: asString(getValue(value, "site_description", "siteDescription")),
    transportOfficerName: asString(getValue(value, "transoffic_name", "transportOfficerName")),
    transportOfficerTelephone: asString(getValue(value, "transoffic_tel", "transportOfficerTelephone")),
    hqReference: asString(getValue(value, "hq_reference", "hqReference")),
    ggReference: asString(getValue(value, "gg_reference", "ggReference")),
    saReference: asString(getValue(value, "sa_reference", "saReference")),
    caseNumber: asString(getValue(value, "case_number", "caseNumber")),
    costOfRepair: asNumber(getValue(value, "cost_of_repair", "costOfRepair")),
    damageDescription: asString(getValue(value, "damage_description", "damageDescription")),
    driverFault: asString(getValue(value, "driver_fault", "driverFault")),
    death: asString(getValue(value, "death")),
    injured: asString(getValue(value, "Injured", "injured")),
    thirdPartyRegistration: asString(getValue(value, "third_party_regno", "thirdPartyRegistration")),
    thirdPartyOwner: asString(getValue(value, "third_party_owner", "thirdPartyOwner")),
    thirdPartyClaim: asNumber(getValue(value, "third_party_claim", "thirdPartyClaim")),
    privateDamagePaymentDate: asString(getValue(value, "priv_dampay_date", "privateDamagePaymentDate")),
    insuranceClaim: asString(getValue(value, "insurance_claim", "insuranceClaim")),
    claimReceived: asString(getValue(value, "th_claim_receive", "claimReceived")),
    claimAgainstDepartment: asNumber(getValue(value, "claim_against_dept", "claimAgainstDepartment")),
    claimDecision: asString(getValue(value, "th_claim_accept_reject", "claimDecision")),
    claimRejectReason: asString(getValue(value, "th_claim_reject_reason", "claimRejectReason")),
    writeOffAmount: asNumber(getValue(value, "write_off_amount", "writeOffAmount")),
    writeOffDate: asString(getValue(value, "write_off_date", "writeOffDate")),
    letterhead: asString(getValue(value, "letterhead")),
    z181: asString(getValue(value, "z181")),
    fileCloseDate: asString(getValue(value, "file_close_date", "fileCloseDate")),
    notes: asString(getValue(value, "notes")),
  };
}

export async function getAccidentVehicleReport(
  searchTerm: string,
  mode: AccidentVehicleReportMode,
) {
  const query = new URLSearchParams({
    searchTerm: searchTerm.trim(),
    mode,
  });
  return mapPresent(getCollection(await requestApi(`api/accidents/reports/vehicle?${query.toString()}`)), mapAccidentVehicleReportRow);
}

export async function getAccidentPrivateVehicleReport(
  searchTerm: string,
  mode: AccidentPrivateVehicleReportMode,
) {
  const query = new URLSearchParams({
    searchTerm: searchTerm.trim(),
    mode,
  });
  return mapPresent(getCollection(await requestApi(`api/accidents/reports/private-vehicle?${query.toString()}`)), mapAccidentVehicleReportRow);
}

export async function getAccidentNewAccidentsReport(mode: AccidentNewAccidentReportMode) {
  const query = new URLSearchParams({ mode });
  return mapPresent(getCollection(await requestApi(`api/accidents/reports/new-accidents?${query.toString()}`)), mapAccidentVehicleReportRow);
}

export async function getAccidentAllReport(mode: AccidentAllReportDateMode) {
  const query = new URLSearchParams({ mode });
  return mapPresent(getCollection(await requestApi(`api/accidents/reports/all?${query.toString()}`)), mapAccidentVehicleReportRow);
}

export async function getAccidentGarageReport(mode: AccidentGarageReportMode) {
  const query = new URLSearchParams({ mode });
  return mapPresent(getCollection(await requestApi(`api/accidents/reports/garage-detail?${query.toString()}`)), mapAccidentVehicleReportRow);
}

export async function getAccidentDepartmentPeriodReport(
  departmentNumber: string,
  startDate: string,
  endDate: string,
) {
  const query = new URLSearchParams({
    departmentNumber: departmentNumber.trim(),
    startDate,
    endDate,
  });
  return mapPresent(
    getCollection(await requestApi(`api/accidents/reports/department-period?${query.toString()}`)),
    mapAccidentVehicleReportRow,
  );
}

export async function getAccidentDepartmentPeriodVipReport(
  departmentNumber: string,
  startDate: string,
  endDate: string,
  mode: AccidentDepartmentPeriodVipMode,
) {
  const query = new URLSearchParams({
    departmentNumber: departmentNumber.trim(),
    startDate,
    endDate,
    mode,
  });
  return mapPresent(
    getCollection(await requestApi(`api/accidents/reports/department-period-vip?${query.toString()}`)),
    mapAccidentVehicleReportRow,
  );
}

function mapAccidentPeriodReportRow(value: unknown): AccidentPeriodReportRow | null {
  if (!isRecord(value)) {
    return null;
  }

  return {
    registrationNumber: asString(getValue(value, "registration_number", "registrationNumber")),
    fleetNumber: asString(getValue(value, "fleet_number", "fleetNumber")),
    accidentDate: asString(getValue(value, "occurence_date", "occurrence_date", "occurenceDate")),
    departmentNumber: asString(getValue(value, "department_number", "departmentNumber")),
    siteDescription: asString(getValue(value, "site_description", "siteDescription")),
    hireType: asString(getValue(value, "hire_type", "hireType")),
    accidentDescription: asString(getValue(value, "accident_description", "accidentDescription", "acc_type_description")),
    driverName: asString(getValue(value, "driver_name", "driverName")),
    transportOfficerName: asString(getValue(value, "transoffic_name", "transportOfficerName")),
    callRefer: asNumber(getValue(value, "Call_Refer", "call_refer", "callRefer")),
    costOfRepair: asNumber(getValue(value, "cost_of_repair", "costOfRepair")),
    fileCloseDate: asString(getValue(value, "file_close_date", "fileCloseDate")),
  };
}

export async function getAccidentPeriodReport(
  departmentNumber: string,
  startDate: string,
  endDate: string,
  status: AccidentPeriodReportStatus,
) {
  const query = new URLSearchParams({
    departmentNumber: departmentNumber.trim(),
    startDate,
    endDate,
    status,
  });
  return mapPresent(getCollection(await requestApi(`api/accidents/reports/period?${query.toString()}`)), mapAccidentPeriodReportRow);
}

export async function updateAccidentAgainstApi(request: AccidentUpdateRequest) {
  await requestApi(`api/accidents/${encodeURIComponent(request.accident_code)}`, {
    method: "PUT",
    body: JSON.stringify(request),
  });

  return { ok: true as const };
}

export async function updateHqAccidentAgainstApi(request: AccidentUpdateRequest) {
  await requestApi(`api/accidents/hq/${encodeURIComponent(request.accident_code)}`, {
    method: "PUT",
    body: JSON.stringify(request),
  });

  return { ok: true as const };
}

export async function deleteAccidentAgainstApi(accidentCode: number) {
  await requestApi(`api/accidents/${encodeURIComponent(accidentCode)}`, {
    method: "DELETE",
  });

  return { ok: true as const };
}

type TypeLookup = {
  code: number;
  description: string;
};

function mapType(value: unknown): TypeLookup | null {
  if (!isRecord(value)) {
    return null;
  }

  const code = asNumber(getValue(value, "type_code", "typeCode"));
  const description = asString(getValue(value, "type_description", "typeDescription"));
  return code !== null && description ? { code, description } : null;
}

function mapAccidentSite(value: unknown): AccidentSiteOption | null {
  if (!isRecord(value)) {
    return null;
  }

  const siteCode = asNumber(getValue(value, "siteCode", "SiteCode", "Site_code", "site_code"));
  if (siteCode === null) {
    return null;
  }

  return {
    siteCode,
    departmentNumber: asString(getValue(value, "departmentNumber", "DepartmentNumber", "Department_number")),
    description: asString(getValue(value, "description", "Description")),
  };
}

function mapAccidentType(value: unknown): AccidentTypeOption | null {
  if (!isRecord(value)) {
    return null;
  }

  const typeCode = asNumber(getValue(value, "acc_type_code", "accTypeCode", "typeCode", "code"));
  const description = asString(
    getValue(value, "acc_type_description", "accTypeDescription", "typeDescription", "description"),
  );
  return typeCode !== null && description ? { typeCode, description } : null;
}

export async function getAccidentReferenceData() {
  const [sitePayload, typePayload] = await Promise.all([
    requestApi("api/site"),
    requestApi("api/accidents/types"),
  ]);

  return {
    sites: mapPresent(getCollection(sitePayload), mapAccidentSite).toSorted((left, right) =>
      `${left.departmentNumber ?? ""} ${left.description ?? ""}`.localeCompare(
        `${right.departmentNumber ?? ""} ${right.description ?? ""}`,
      ),
    ),
    accidentTypes: mapPresent(getCollection(typePayload), mapAccidentType).toSorted((left, right) =>
      left.description.localeCompare(right.description),
    ),
  };
}

type AccidentLookup = {
  accidentCode: number;
  vmfCode: number | null;
  accidentDate: string | null;
  reference: string | null;
};

function mapAccident(value: unknown): AccidentLookup | null {
  if (!isRecord(value)) {
    return null;
  }

  const accidentCode = asNumber(getValue(value, "accident_code", "accidentCode"));
  if (accidentCode === null) {
    return null;
  }

  return {
    accidentCode,
    vmfCode: asNumber(getValue(value, "vmf_code", "vmfCode")),
    accidentDate: asString(getValue(value, "occurence_date", "occurrence_date", "occurenceDate", "accident_date")),
    reference: asString(getValue(value, "gg_reference", "ggReference")),
  };
}

export async function getGarageAccidentPage(
  page: number,
  searchType: GarageSearchType,
  searchTerm: string,
  pageSize = 12,
): Promise<GarageAccidentPage> {
  const normalizedSearchTerm = searchTerm.trim();
  const vehiclePath = normalizedSearchTerm
    ? `api/vehicles/search?searchTerm=${encodeURIComponent(normalizedSearchTerm)}`
    : "api/vehicles";

  const [accidentPayload, vehiclePayload, typePayload] = await Promise.all([
    requestApi("api/accidents"),
    requestApi(vehiclePath),
    requestApi("api/type"),
  ]);

  const vehicles = mapPresent(getCollection(vehiclePayload), mapVehicle);
  const vehicleByCode = new Map(vehicles.map((vehicle) => [vehicle.vmfCode, vehicle]));
  const typesByCode = new Map<number, string>();
  for (const type of mapPresent(getCollection(typePayload), mapType)) {
    typesByCode.set(type.code, type.description);
  }

  const matchingVehicleCodes = normalizedSearchTerm ? new Set<number>() : null;
  if (matchingVehicleCodes) {
    const normalizedLowerTerm = normalizedSearchTerm.toLocaleLowerCase();
    for (const vehicle of vehicles) {
      const value = searchType === "GG" ? vehicle.fleetNumber : vehicle.registrationNumber;
      if ((value?.trim().toLocaleLowerCase() ?? "") === normalizedLowerTerm) {
        matchingVehicleCodes.add(vehicle.vmfCode);
      }
    }
  }

  const allRows = mapPresent(getCollection(accidentPayload), mapAccident).reduce<GarageAccidentRow[]>((rows, accident) => {
    if (matchingVehicleCodes !== null && (accident.vmfCode === null || !matchingVehicleCodes.has(accident.vmfCode))) {
      return rows;
    }

      const vehicle = accident.vmfCode === null ? undefined : vehicleByCode.get(accident.vmfCode);
      const vehicleNumber = searchType === "GG" ? vehicle?.fleetNumber : vehicle?.registrationNumber;

      rows.push({
        accidentCode: accident.accidentCode,
        vehicleNumber: vehicleNumber ?? null,
        hireType: vehicle?.typeCode === null || vehicle?.typeCode === undefined ? null : typesByCode.get(vehicle.typeCode) ?? null,
        accidentDate: accident.accidentDate,
        reference: accident.reference,
      });
      return rows;
    }, [])
    .toSorted((left, right) => (right.accidentDate ?? "").localeCompare(left.accidentDate ?? ""));

  const totalRecords = allRows.length;
  const totalPages = Math.max(1, Math.ceil(totalRecords / pageSize));
  const safePage = Math.min(Math.max(page, 1), totalPages);

  return {
    rows: allRows.slice((safePage - 1) * pageSize, safePage * pageSize),
    page: safePage,
    pageSize,
    totalRecords,
    totalPages,
    searchTerm: normalizedSearchTerm,
    searchType,
  };
}

export async function getHqAccidentPage(
  page: number,
  searchType: GarageSearchType,
  searchTerm: string,
  pageSize = 12,
) {
  return getGarageAccidentPage(page, searchType, searchTerm, pageSize);
}

export async function createAccidentAgainstApi(request: CreateAccidentRequest) {
  await requestApi("api/accidents", {
    method: "POST",
    body: JSON.stringify(request),
  });

  return { ok: true as const };
}

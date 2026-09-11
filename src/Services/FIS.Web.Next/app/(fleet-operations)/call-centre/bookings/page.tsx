import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import { saveBookingAction } from "@/app/(fleet-operations)/call-centre/bookings/actions";
import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import { StreamedRoute } from "@/components/app-shell/streamed-route";
import {
  CallCentreApiError,
  getCallCentreIncident,
  getCallCentreSites,
  type CallCentreSiteOption,
} from "@/lib/api/fleet-operations/api-call-centre";
import { getClasses, type ClassRecord } from "@/lib/api/reference-data/api-classes";
import {
  BookingApiError,
  getBooking,
  type BookingRecord,
} from "@/lib/api/fleet-operations/api-bookings";
import {
  getUserAdminUserChoices,
  type UserAdminProfile,
} from "@/lib/api/administration/api-user-admin";
import { getSession } from "@/lib/auth/session";

const CALL_CENTRE_ROLE = "Call Centre";
type SearchParams = Promise<Record<string, string | string[] | undefined>>;
type SelectOption = { value: string; label: string };
type BookingLookups = {
  classOptions: SelectOption[];
  siteOptions: SelectOption[];
  userOptions: SelectOption[];
  blockers: string[];
};

export type BookingPageProps = { searchParams: SearchParams };

function queryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

function positiveInt(value: string | undefined) {
  const parsed = Number(value);
  return value && Number.isInteger(parsed) && parsed > 0 ? parsed : null;
}

function hasRole(roles: readonly string[], role: string) {
  return roles.some(
    (candidate) => candidate.localeCompare(role, undefined, { sensitivity: "accent" }) === 0,
  );
}

function valueOrEmpty(value: string | number | null | undefined) {
  return value === null || value === undefined ? "" : String(value);
}

function dateTimeInputValue(value: string | null | undefined) {
  return value?.match(/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}/)?.[0] ?? "";
}

function nowDateTimeInputValue() {
  return new Date().toISOString().slice(0, 16);
}

function emptyBooking(): BookingRecord {
  const now = nowDateTimeInputValue();
  return {
    bookingId: 0,
    siteCode: null,
    name: null,
    startDate: now,
    endDate: now,
    classCode: 0,
    userId: 0,
    bookingDate: now,
    telephone: null,
    collected: 0,
    locationCode: 0,
    vmfCode: null,
    bookingStatus: "Pending",
    notes: null,
    dateCreated: null,
    dateUpdated: null,
    createdByUserCode: null,
    modifiedByUserCode: null,
    isDeleted: false,
  };
}

function namedOption(code: number, name: string | null | undefined): SelectOption | null {
  const normalizedName = name?.trim();
  return normalizedName ? { value: String(code), label: `${normalizedName} (${code})` } : null;
}

function classOptions(classes: readonly ClassRecord[]) {
  return classes
    .map((item) => namedOption(item.classCode, item.description))
    .filter((item): item is SelectOption => item !== null)
    .sort((left, right) => left.label.localeCompare(right.label));
}

function siteOptions(sites: readonly CallCentreSiteOption[]) {
  return sites
    .map((site) => namedOption(site.code, site.description))
    .filter((item): item is SelectOption => item !== null)
    .sort((left, right) => left.label.localeCompare(right.label));
}

function userDisplayName(user: UserAdminProfile) {
  const fullName = [user.firstName, user.lastName].filter(Boolean).join(" ").trim();
  return fullName || user.userName?.trim() || null;
}

function userOptions(users: readonly UserAdminProfile[]) {
  return users
    .map((user) => namedOption(user.userAccessCode, userDisplayName(user)))
    .filter((item): item is SelectOption => item !== null)
    .sort((left, right) => left.label.localeCompare(right.label));
}

async function getBookingLookups(): Promise<BookingLookups> {
  const [classesResult, sitesResult, usersResult] = await Promise.allSettled([
    getClasses(),
    getCallCentreSites(),
    getUserAdminUserChoices(),
  ]);
  const classes = classesResult.status === "fulfilled" ? classesResult.value : [];
  const sites = sitesResult.status === "fulfilled" ? sitesResult.value : [];
  const users = usersResult.status === "fulfilled" ? usersResult.value : [];
  const classChoices = classOptions(classes);
  const siteChoices = siteOptions(sites);
  const userChoices = userOptions(users);
  const blockers: string[] = [];

  if (classesResult.status === "rejected" || classChoices.length === 0) {
    blockers.push("Class Code options are unavailable, so the code remains manual.");
  }
  if (sitesResult.status === "rejected" || siteChoices.length === 0) {
    blockers.push("Site Code options are unavailable, so the code remains manual.");
  }
  if (usersResult.status === "rejected" || userChoices.length === 0) {
    blockers.push("User ID options are unavailable, so the code remains manual.");
  }

  return {
    classOptions: classChoices,
    siteOptions: siteChoices,
    userOptions: userChoices,
    blockers,
  };
}

function hasOption(options: readonly SelectOption[], value: string) {
  return options.some((option) => option.value === value);
}

function canUseSelect(options: readonly SelectOption[], value: string, isEdit: boolean) {
  if (options.length === 0) return false;
  if (!isEdit && (value === "" || value === "0")) return true;
  return value === "" || hasOption(options, value);
}

function selectDefaultValue(value: string, isEdit: boolean) {
  return !isEdit && value === "0" ? "" : value;
}

function currentLookupBlockers(record: BookingRecord, lookups: BookingLookups) {
  const blockers = [...lookups.blockers];
  if (record.bookingId <= 0) return blockers;

  const currentValues = [
    ["Class Code", valueOrEmpty(record.classCode), lookups.classOptions],
    ["Site Code", valueOrEmpty(record.siteCode), lookups.siteOptions],
    ["User ID", valueOrEmpty(record.userId), lookups.userOptions],
  ] as const;
  for (const [label, value, options] of currentValues) {
    if (value && options.length > 0 && !hasOption(options, value)) {
      blockers.push(`${label} ${value} has no named lookup match, so it remains manual.`);
    }
  }

  return blockers;
}

function AccessRestricted() {
  return (
    <section className="vehicle-status-card" role="alert">
      <p className="eyebrow">Access restricted</p>
      <h2>You do not have permission to manage bookings.</h2>
      <p className="muted-copy">This page requires the Call Centre role.</p>
    </section>
  );
}

function ApiUnavailable() {
  return (
    <section className="vehicle-status-card" role="alert">
      <p className="eyebrow">Service unavailable</p>
      <h2>The booking workflow could not be opened.</h2>
      <p className="muted-copy">
        The application is still running. Retry when the FIS API is available.
      </p>
      <div className="button-row">
        <Link className="button button-primary" href="/call-centre/bookings">
          Try again
        </Link>
        <Link className="button button-secondary" href="/call-centre">
          Back to Call Centre
        </Link>
      </div>
    </section>
  );
}

function LookupForm({ bookingId, sourceGmt }: Readonly<{ bookingId: string; sourceGmt: string }>) {
  const newBookingHref = sourceGmt
    ? `/call-centre/bookings?gmt=${encodeURIComponent(sourceGmt)}`
    : "/call-centre/bookings";

  return (
    <section className="vehicle-form-section" aria-labelledby="booking-lookup-title">
      <div className="vehicle-form-section-header">
        <div>
          <p className="eyebrow">Booking section</p>
          <h2 id="booking-lookup-title">Open a booking</h2>
        </div>
      </div>
      <form method="get" className="form-stack">
        <div className="field">
          <label htmlFor="booking-lookup-id">Existing Booking ID</label>
          <input
            id="booking-lookup-id"
            name="bookingId"
            inputMode="numeric"
            defaultValue={bookingId}
          />
          {sourceGmt ? <input type="hidden" name="gmt" value={sourceGmt} /> : null}
        </div>
        <div className="button-row">
          <button className="button button-secondary" type="submit">
            Load Booking
          </button>
          <Link className="button button-primary" href={newBookingHref}>
            New Booking
          </Link>
          <Link className="button button-secondary" href="/call-centre">
            Menu
          </Link>
        </div>
      </form>
    </section>
  );
}

function Field({
  id,
  label,
  name,
  defaultValue,
  type = "text",
  required = false,
}: Readonly<{
  id: string;
  label: string;
  name: string;
  defaultValue: string;
  type?: string;
  required?: boolean;
}>) {
  return (
    <div className="field">
      <label htmlFor={id}>{label}</label>
      <input id={id} name={name} type={type} defaultValue={defaultValue} required={required} />
    </div>
  );
}

function SelectField({
  id,
  label,
  name,
  defaultValue,
  options,
  placeholder,
  required = false,
}: Readonly<{
  id: string;
  label: string;
  name: string;
  defaultValue: string;
  options: readonly SelectOption[];
  placeholder: string;
  required?: boolean;
}>) {
  return (
    <div className="field">
      <label htmlFor={id}>{label}</label>
      <select id={id} name={name} defaultValue={defaultValue} required={required}>
        <option value="">{placeholder}</option>
        {options.map((option) => (
          <option key={option.value} value={option.value}>
            {option.label}
          </option>
        ))}
      </select>
    </div>
  );
}

function BookingForm({
  record,
  sourceGmt,
  lookups,
}: Readonly<{
  record: BookingRecord;
  sourceGmt: string;
  lookups: BookingLookups;
}>) {
  const isEdit = record.bookingId > 0;
  const siteValue = valueOrEmpty(record.siteCode);
  const classValue = valueOrEmpty(record.classCode);
  const userValue = valueOrEmpty(record.userId);

  return (
    <form action={saveBookingAction} className="vehicle-form-stack">
      {isEdit ? <input name="booking_id" type="hidden" value={record.bookingId} /> : null}
      {sourceGmt ? <input name="sourceGmt" type="hidden" value={sourceGmt} /> : null}
      <section className="vehicle-form-section" aria-labelledby="booking-form-title">
        <div className="vehicle-form-section-header">
          <div>
            <p className="eyebrow">{isEdit ? `Booking #${record.bookingId}` : "New booking"}</p>
            <h2 id="booking-form-title">{isEdit ? "Edit Booking" : "Capture Booking"}</h2>
          </div>
        </div>
        <div className="field-grid">
          <Field
            id="booking-name"
            label="Name"
            name="name"
            defaultValue={valueOrEmpty(record.name)}
          />
          {canUseSelect(lookups.siteOptions, siteValue, isEdit) ? (
            <SelectField
              id="booking-site"
              label="Site"
              name="site_code"
              defaultValue={selectDefaultValue(siteValue, isEdit)}
              options={lookups.siteOptions}
              placeholder="Select site"
            />
          ) : (
            <Field id="booking-site" label="Site Code" name="site_code" defaultValue={siteValue} />
          )}
          <Field
            id="booking-telephone"
            label="Telephone"
            name="telephone"
            defaultValue={valueOrEmpty(record.telephone)}
          />
          <Field
            id="booking-vmf"
            label="VMF Code"
            name="vmf_code"
            type="number"
            defaultValue={valueOrEmpty(record.vmfCode)}
          />
          {canUseSelect(lookups.classOptions, classValue, isEdit) ? (
            <SelectField
              id="booking-class"
              label="Vehicle class"
              name="class_code"
              defaultValue={selectDefaultValue(classValue, isEdit)}
              options={lookups.classOptions}
              placeholder="Select class"
              required
            />
          ) : (
            <Field
              id="booking-class"
              label="Class Code"
              name="class_code"
              type="number"
              defaultValue={classValue}
              required
            />
          )}
          {canUseSelect(lookups.userOptions, userValue, isEdit) ? (
            <SelectField
              id="booking-user"
              label="Requesting user"
              name="user_id"
              defaultValue={selectDefaultValue(userValue, isEdit)}
              options={lookups.userOptions}
              placeholder="Select user"
              required
            />
          ) : (
            <Field
              id="booking-user"
              label="User ID"
              name="user_id"
              type="number"
              defaultValue={userValue}
              required
            />
          )}
          <Field
            id="booking-location"
            label="Location Code"
            name="location_code"
            type="number"
            defaultValue={valueOrEmpty(record.locationCode)}
            required
          />
          <Field
            id="booking-start"
            label="Start Date"
            name="start_date"
            type="datetime-local"
            defaultValue={dateTimeInputValue(record.startDate)}
            required
          />
          <Field
            id="booking-end"
            label="End Date"
            name="end_date"
            type="datetime-local"
            defaultValue={dateTimeInputValue(record.endDate)}
          />
          <Field
            id="booking-status"
            label="Booking Status"
            name="booking_status"
            defaultValue={valueOrEmpty(record.bookingStatus)}
          />
          <Field
            id="booking-collected"
            label="Collected (0/1)"
            name="collected"
            type="number"
            defaultValue={valueOrEmpty(record.collected)}
          />
          <Field
            id="booking-date"
            label="Booking Date"
            name="booking_date"
            type="datetime-local"
            defaultValue={dateTimeInputValue(record.bookingDate)}
            required
          />
          <div className="field field-full">
            <label htmlFor="booking-notes">Notes</label>
            <textarea
              id="booking-notes"
              name="notes"
              rows={3}
              defaultValue={valueOrEmpty(record.notes)}
            />
          </div>
        </div>
        <div className="button-row">
          <button className="button button-primary" type="submit">
            Submit
          </button>
          <Link
            className="button button-secondary"
            href={
              sourceGmt
                ? `/call-centre/bookings?gmt=${encodeURIComponent(sourceGmt)}`
                : "/call-centre/bookings"
            }
          >
            Clear
          </Link>
        </div>
      </section>
      {isEdit ? (
        <section className="vehicle-form-section" aria-labelledby="booking-audit-title">
          <div className="vehicle-form-section-header">
            <div>
              <p className="eyebrow">Compatibility metadata</p>
              <h2 id="booking-audit-title">Record metadata</h2>
            </div>
          </div>
          <div className="field-grid">
            <p className="form-hint">
              Created: {valueOrEmpty(record.dateCreated) || "Not present on this legacy schema"}
            </p>
            <p className="form-hint">
              Updated: {valueOrEmpty(record.dateUpdated) || "Not present on this legacy schema"}
            </p>
            <p className="form-hint">
              Created by:{" "}
              {valueOrEmpty(record.createdByUserCode) || "Not present on this legacy schema"}
            </p>
            <p className="form-hint">
              Modified by:{" "}
              {valueOrEmpty(record.modifiedByUserCode) || "Not present on this legacy schema"}
            </p>
            <p className="form-hint">Deleted: {record.isDeleted ? "Yes" : "No"}</p>
          </div>
        </section>
      ) : null}
    </form>
  );
}

function errorMessage(error: unknown) {
  if (error instanceof BookingApiError || error instanceof CallCentreApiError) {
    if (error.reason === "unauthorized")
      return "Your session has expired. Sign in again before continuing.";
    if (error.reason === "not-found")
      return "The requested booking or GMT reference was not found.";
    if (error.reason === "unavailable")
      return "The booking service is temporarily unavailable. Please try again.";
  }

  return "The booking workflow could not load the requested record.";
}

const BookingPageContent = renderBookingPageContent;

async function renderBookingPageContent({ searchParams }: BookingPageProps) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired") {
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath="/call-centre/bookings" />
      </main>
    );
  }
  if (session.status === "unavailable") {
    return (
      <main className="page-shell vehicle-page-shell">
        <ApiUnavailable />
      </main>
    );
  }
  if (!hasRole(session.roles, CALL_CENTRE_ROLE)) {
    return (
      <main className="page-shell vehicle-page-shell">
        <AccessRestricted />
      </main>
    );
  }

  const query = await searchParams;
  const rawGmt = (queryValue(query.gmt) ?? queryValue(query.GMT) ?? "").trim();
  const rawBookingId = (queryValue(query.bookingId) ?? queryValue(query.booking_id) ?? "").trim();
  const error = queryValue(query.error);
  const saved = queryValue(query.saved) === "1";
  const linkWarning = queryValue(query.linkWarning) === "1";
  const gmt = positiveInt(rawGmt);
  const bookingId = positiveInt(rawBookingId);
  let record: BookingRecord | null = null;
  let sourceGmt = gmt ? String(gmt) : "";
  let loadError = "";
  let contextNotice = "";

  try {
    if (gmt) {
      const incident = await getCallCentreIncident(gmt);
      const linkedBookingId = positiveInt(incident.incidentDescription ?? undefined);
      if (linkedBookingId) {
        record = await getBooking(linkedBookingId);
        if (!record) loadError = `Booking ID ${linkedBookingId} was not found for GMT ${gmt}.`;
      } else {
        contextNotice = `No linked booking ID was found on GMT ${gmt}. You can capture a new booking here.`;
      }
    } else if (rawGmt) {
      contextNotice = "Enter a valid numeric GMT reference to open its linked booking.";
      sourceGmt = rawGmt;
    } else if (bookingId) {
      record = await getBooking(bookingId);
      if (!record) loadError = `Booking ID ${bookingId} was not found.`;
    }
  } catch (caughtError) {
    loadError = errorMessage(caughtError);
  }

  const formRecord = record ?? emptyBooking();
  const lookups = await getBookingLookups();
  const lookupBlockers = currentLookupBlockers(formRecord, lookups);

  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-labelledby="booking-page-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Call Centre / Booking Section</p>
            <h1 id="booking-page-title">Call Centre Booking Workflow</h1>
            <p>Capture new bookings or edit existing bookings linked from call centre incidents.</p>
          </div>
          <Link className="button button-secondary" href="/call-centre">
            Call Centre Menu
          </Link>
        </header>
        {error ? (
          <div className="notice notice-error" role="alert">
            {error}
          </div>
        ) : null}
        {saved ? (
          <div className="notice notice-success" role="status">
            Booking #{formRecord.bookingId || bookingId} saved successfully.
          </div>
        ) : null}
        {linkWarning ? (
          <div className="notice notice-error" role="alert">
            The booking was saved, but the source GMT link could not be updated. The booking can
            still be opened by ID.
          </div>
        ) : null}
        {contextNotice ? (
          <div className="notice notice-info" role="status">
            {contextNotice}
          </div>
        ) : null}
        {lookupBlockers.length > 0 ? (
          <div className="notice notice-info" role="status">
            Some code fields remain manual: {lookupBlockers.join(" ")}
          </div>
        ) : null}
        <LookupForm bookingId={rawBookingId} sourceGmt={sourceGmt} />
        {loadError ? (
          <div className="notice notice-error" role="alert">
            {loadError}
          </div>
        ) : null}
        <BookingForm record={formRecord} sourceGmt={sourceGmt} lookups={lookups} />
      </section>
    </main>
  );
}

export default function BookingPage(props: BookingPageProps) {
  return (
    <StreamedRoute>
      <BookingPageContent {...props} />
    </StreamedRoute>
  );
}

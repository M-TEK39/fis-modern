"use client";

import Link from "next/link";
import { useActionState } from "react";
import { useFormStatus } from "react-dom";

import type { NoticeActionState } from "@/app/notice-management/actions";

type NoticeAction = (
  previousState: NoticeActionState,
  formData: FormData,
) => Promise<NoticeActionState>;

type Props = {
  action: NoticeAction;
  returnPath: string;
  noticeId: number;
  scheduleId: number;
  titleField: string;
  noticeDate: string;
  noticeFrom: string;
  noticeTitle: string;
  noticeBody: string;
  noticePerson: string;
  noticePersonTitle: string;
  scheduleStart: string;
  scheduleEnd: string;
  sortOrder: number;
  createdDate: string;
  saved: boolean;
};

const initialState: NoticeActionState = { status: "idle" };

function SubmitButton({ editing }: Readonly<{ editing: boolean }>) {
  const { pending } = useFormStatus();
  return (
    <button className="button button-primary" type="submit" disabled={pending}>
      {pending ? "Saving..." : editing ? "Update Notice" : "Create Notice"}
    </button>
  );
}

function Field({
  id,
  label,
  name,
  defaultValue,
  type = "text",
  required = false,
  maxLength,
}: Readonly<{
  id: string;
  label: string;
  name: string;
  defaultValue: string;
  type?: "text" | "date" | "number";
  required?: boolean;
  maxLength?: number;
}>) {
  return (
    <div className="field">
      <label htmlFor={id}>
        {label} {required ? <span aria-hidden="true">*</span> : null}
        {required ? <span className="sr-only"> required</span> : null}
      </label>
      <input
        id={id}
        name={name}
        type={type}
        defaultValue={defaultValue}
        maxLength={maxLength}
        required={required}
      />
    </div>
  );
}

export default function NoticeDetailForm({
  action,
  returnPath,
  noticeId,
  scheduleId,
  titleField,
  noticeDate,
  noticeFrom,
  noticeTitle,
  noticeBody,
  noticePerson,
  noticePersonTitle,
  scheduleStart,
  scheduleEnd,
  sortOrder,
  createdDate,
  saved,
}: Readonly<Props>) {
  const [state, formAction] = useActionState(action, initialState);
  const editing = noticeId > 0 || scheduleId > 0;

  return (
    <form action={formAction} className="vehicle-create-form">
      <input name="returnPath" type="hidden" value={returnPath} readOnly />
      <input name="noticeId" type="hidden" value={noticeId || ""} readOnly />
      <input name="scheduleId" type="hidden" value={scheduleId || ""} readOnly />
      <input name="titleField" type="hidden" value={titleField} readOnly />

      {saved ? (
        <div className="notice notice-success" role="status">
          <span aria-hidden="true">✓</span>
          <span>Notice saved successfully.</span>
        </div>
      ) : null}
      {state.status === "error" && state.message ? (
        <div className="notice notice-error" role="alert">
          <span aria-hidden="true">!</span>
          <span>{state.message}</span>
        </div>
      ) : null}

      <section className="vehicle-form-section" aria-labelledby="notice-schedule-title">
        <div className="vehicle-form-section-header">
          <div>
            <p className="eyebrow">Notice schedule</p>
            <h2 id="notice-schedule-title">Display timing</h2>
          </div>
          <span className="vehicle-required-note">* Required</span>
        </div>
        <div className="vehicle-create-grid">
          <Field id="notice-schedule-start" label="Starting display date" name="scheduleStart" type="date" defaultValue={scheduleStart} />
          <Field id="notice-schedule-end" label="Ending display date" name="scheduleEnd" type="date" defaultValue={scheduleEnd} />
          <Field id="notice-schedule-order" label="Display order" name="sortOrder" type="number" defaultValue={String(sortOrder)} />
        </div>
      </section>

      <section className="vehicle-form-section" aria-labelledby="notice-communication-title">
        <div className="vehicle-form-section-header">
          <div>
            <p className="eyebrow">Client communication</p>
            <h2 id="notice-communication-title">Notice details</h2>
          </div>
        </div>
        <div className="vehicle-create-grid">
          <Field id="notice-date" label="Date" name="noticeDate" type="date" defaultValue={noticeDate} />
          <Field id="notice-from" label="From" name="noticeFrom" defaultValue={noticeFrom} required maxLength={255} />
          <div className="field field-wide">
            <label htmlFor="notice-title">
              Title <span aria-hidden="true">*</span><span className="sr-only"> required</span>
            </label>
            <input id="notice-title" name="noticeTitle" type="text" defaultValue={noticeTitle} maxLength={255} required />
          </div>
          <div className="field field-wide">
            <label htmlFor="notice-body">Body</label>
            <textarea id="notice-body" name="noticeBody" defaultValue={noticeBody} rows={7} />
          </div>
          <Field id="notice-person" label="Responsible person" name="noticePerson" defaultValue={noticePerson} required maxLength={255} />
          <Field id="notice-person-title" label="Person's title" name="noticePersonTitle" defaultValue={noticePersonTitle} maxLength={255} />
          <div className="field">
            <label htmlFor="notice-created-date">Created date</label>
            <input id="notice-created-date" type="date" value={createdDate} disabled readOnly />
          </div>
        </div>
      </section>

      <div className="vehicle-create-actions">
        <Link className="button button-secondary" href={returnPath === "/Admin/NoticeDetailManagement.aspx" ? "/Admin/NoticeManagement.aspx" : "/notice-management"}>
          Back to main management screen
        </Link>
        <SubmitButton editing={editing} />
      </div>
    </form>
  );
}

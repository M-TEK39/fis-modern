"use client";

import { useState, type FormEvent } from "react";

import type { VehicleAuthorizationActionState } from "./actions";
import type { VehicleAuthorization } from "@/lib/api/vehicles/api-vehicle-authorization";

export default function ReviewModalActionForm({
  vehicle,
  pending,
  actionState,
  formAction,
  onClose,
}: Readonly<{
  vehicle: VehicleAuthorization;
  pending: boolean;
  actionState: VehicleAuthorizationActionState;
  formAction: (payload: FormData) => void;
  onClose: () => void;
}>) {
  const [comment, setComment] = useState("");
  const [formError, setFormError] = useState<string | null>(null);

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    const submitter = (event.nativeEvent as SubmitEvent).submitter as HTMLButtonElement | null;
    const intent = submitter?.value;

    if (!intent || !["approve", "reject", "comment"].includes(intent)) {
      return;
    }

    if (!comment.trim()) {
      event.preventDefault();
      setFormError(
        intent === "comment"
          ? "Comment cannot be empty."
          : "Please supply a comment before continuing.",
      );
      return;
    }

    const maximumCommentLength = intent === "reject" ? 244 : 255;
    if (comment.trim().length > maximumCommentLength) {
      event.preventDefault();
      setFormError(`Authorizer comment must be ${maximumCommentLength} characters or fewer.`);
      return;
    }

    setFormError(null);
  }

  return (
    <form action={formAction} onSubmit={handleSubmit} className="vehicle-review-form">
      <input type="hidden" name="id" value={vehicle.tempVmfCode} />
      {actionState.status === "error" && actionState.message ? (
        <div className="notice notice-error" role="alert">
          <span aria-hidden="true">!</span>
          <span>{actionState.message}</span>
        </div>
      ) : null}
      {formError ? (
        <div className="notice notice-error" role="alert">
          {formError}
        </div>
      ) : null}
      <div className="field">
        <label htmlFor="vehicle-authorizer-comment">
          Authorizer&apos;s Comment <span aria-hidden="true">*</span>
        </label>
        <textarea
          id="vehicle-authorizer-comment"
          name="comment"
          rows={3}
          value={comment}
          onChange={(event) => setComment(event.target.value)}
          maxLength={255}
          required
        />
      </div>
      <div className="vehicle-create-actions">
        <button
          className="button button-secondary"
          type="button"
          onClick={onClose}
          disabled={pending}
        >
          Close
        </button>
        <button
          className="button button-secondary"
          type="submit"
          name="intent"
          value="comment"
          disabled={pending}
        >
          {pending ? "Saving..." : "Add Comment"}
        </button>
        <button
          className="button button-primary"
          type="submit"
          name="intent"
          value="approve"
          disabled={pending}
        >
          {pending ? "Saving..." : "Approve"}
        </button>
        <button
          className="button button-danger"
          type="submit"
          name="intent"
          value="reject"
          disabled={pending}
        >
          {pending ? "Saving..." : "Reject"}
        </button>
      </div>
    </form>
  );
}

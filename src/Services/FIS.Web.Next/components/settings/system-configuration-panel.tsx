"use client";

import * as React from "react";
import { CheckCircle2, CircleAlert, RefreshCw, ShieldCheck } from "lucide-react";

import {
  getSystemConfigurationAction,
  saveSystemConfigurationAction,
} from "@/app/_actions/system-configuration";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import {
  AuthenticationConfigurationCard,
  SessionConfigurationCard,
  type SystemFormState,
} from "@/components/settings/system-configuration-sections";
import type {
  SystemConfigurationStatus,
  SystemConfigurationUpdate,
} from "@/lib/api/administration/api-system-configuration";

const MIN_REFRESH_LIFETIME_MINUTES = 15;
const MAX_REFRESH_LIFETIME_MINUTES = 30 * 24 * 60;

type FormState = SystemFormState;

type Message = { text: string; tone: "success" | "error" };

function fromStatus(status: SystemConfigurationStatus): FormState {
  return {
    standardRefreshLifetimeMinutes: String(status.session.standardRefreshLifetimeMinutes),
    rememberRefreshLifetimeMinutes: String(status.session.rememberRefreshLifetimeMinutes),
    entraEnabled: status.authentication.entraEnabled,
    tenantId: status.authentication.tenantId ?? "",
    clientId: status.authentication.clientId ?? "",
    clientSecret: "",
    passwordResetSigningKey: "",
  };
}

function StatusMessage({ message }: Readonly<{ message: Message | null }>) {
  if (!message) return null;
  const isError = message.tone === "error";
  return (
    <div
      className={`flex items-start gap-2 rounded-lg border p-3 text-sm ${
        isError
          ? "border-destructive/40 bg-destructive/5 text-destructive"
          : "border-primary/30 bg-primary/5"
      }`}
      role={isError ? "alert" : "status"}
      aria-live="polite"
    >
      {isError ? (
        <CircleAlert className="mt-0.5 size-4 shrink-0" aria-hidden="true" />
      ) : (
        <CheckCircle2 className="mt-0.5 size-4 shrink-0" aria-hidden="true" />
      )}
      <span>{message.text}</span>
    </div>
  );
}

function LoadingState() {
  return (
    <div
      className="rounded-lg border border-dashed p-4 text-sm text-muted-foreground"
      role="status"
    >
      Loading secure system configuration…
    </div>
  );
}

function ErrorState({ message, onRetry }: Readonly<{ message: string; onRetry: () => void }>) {
  return (
    <div
      className="grid gap-3 rounded-lg border border-destructive/40 bg-destructive/5 p-4 text-sm"
      role="alert"
    >
      <div className="flex items-start gap-2 text-destructive">
        <CircleAlert className="mt-0.5 size-4 shrink-0" aria-hidden="true" />
        <p>{message}</p>
      </div>
      <Button className="w-fit" size="sm" variant="outline" onClick={onRetry}>
        <RefreshCw aria-hidden="true" />
        Retry
      </Button>
    </div>
  );
}

function makeSessionUpdate(
  form: FormState,
  status: SystemConfigurationStatus,
): SystemConfigurationUpdate | null {
  const standard = Number(form.standardRefreshLifetimeMinutes);
  const remember = Number(form.rememberRefreshLifetimeMinutes);
  if (
    !Number.isInteger(standard) ||
    standard < MIN_REFRESH_LIFETIME_MINUTES ||
    standard > MAX_REFRESH_LIFETIME_MINUTES ||
    !Number.isInteger(remember) ||
    remember < MIN_REFRESH_LIFETIME_MINUTES ||
    remember > MAX_REFRESH_LIFETIME_MINUTES
  )
    return null;

  const session: NonNullable<SystemConfigurationUpdate["session"]> = {};
  if (standard !== status.session.standardRefreshLifetimeMinutes)
    session.standardRefreshLifetimeMinutes = standard;
  if (remember !== status.session.rememberRefreshLifetimeMinutes)
    session.rememberRefreshLifetimeMinutes = remember;
  return Object.keys(session).length > 0 ? { session } : null;
}

function makeAuthenticationUpdate(
  form: FormState,
  status: SystemConfigurationStatus,
): SystemConfigurationUpdate | null {
  const authentication: NonNullable<SystemConfigurationUpdate["authentication"]> = {};
  const tenantId = form.tenantId.trim();
  const clientId = form.clientId.trim();
  const clientSecret = form.clientSecret.trim();
  const passwordResetSigningKey = form.passwordResetSigningKey.trim();

  if (form.entraEnabled !== status.authentication.entraEnabled)
    authentication.entraEnabled = form.entraEnabled;
  if (tenantId && tenantId !== (status.authentication.tenantId ?? ""))
    authentication.tenantId = tenantId;
  if (clientId && clientId !== (status.authentication.clientId ?? ""))
    authentication.clientId = clientId;
  if (clientSecret) authentication.clientSecret = clientSecret;
  if (passwordResetSigningKey) authentication.passwordResetSigningKey = passwordResetSigningKey;

  return Object.keys(authentication).length > 0 ? { authentication } : null;
}

function hasSessionChanges(form: FormState, status: SystemConfigurationStatus) {
  return (
    form.standardRefreshLifetimeMinutes !== String(status.session.standardRefreshLifetimeMinutes) ||
    form.rememberRefreshLifetimeMinutes !== String(status.session.rememberRefreshLifetimeMinutes)
  );
}

function hasAuthenticationChanges(form: FormState, status: SystemConfigurationStatus) {
  return (
    form.entraEnabled !== status.authentication.entraEnabled ||
    (form.tenantId.trim().length > 0 &&
      form.tenantId.trim() !== (status.authentication.tenantId ?? "")) ||
    (form.clientId.trim().length > 0 &&
      form.clientId.trim() !== (status.authentication.clientId ?? "")) ||
    form.clientSecret.trim().length > 0 ||
    form.passwordResetSigningKey.trim().length > 0
  );
}

export function SystemConfigurationPanel({
  section = "all",
}: Readonly<{ section?: "all" | "session" | "authentication" }>) {
  const [status, setStatus] = React.useState<SystemConfigurationStatus | null>(null);
  const [form, setForm] = React.useState<FormState | null>(null);
  const [loadError, setLoadError] = React.useState<string | null>(null);
  const [message, setMessage] = React.useState<Message | null>(null);
  const [saving, startSaving] = React.useTransition();

  const refresh = React.useCallback(async () => {
    setLoadError(null);
    const result = await getSystemConfigurationAction();
    if (!result.ok) {
      setLoadError(result.message);
      return;
    }
    setStatus(result.status);
    setForm(fromStatus(result.status));
  }, []);

  React.useEffect(() => {
    void refresh();
  }, [refresh]);

  const save = (update: SystemConfigurationUpdate | null, emptyMessage: string) => {
    if (!update) {
      setMessage({ text: emptyMessage, tone: "error" });
      return;
    }

    startSaving(async () => {
      const result = await saveSystemConfigurationAction(update);
      setMessage({
        text: result.message ?? (result.ok ? "System configuration saved." : "Save failed."),
        tone: result.ok ? "success" : "error",
      });
      if (result.ok) await refresh();
    });
  };

  if (loadError) return <ErrorState message={loadError} onRetry={() => void refresh()} />;
  if (!status || !form) return <LoadingState />;

  const managementAvailable = status.configurationManagementAvailable;
  const sessionChanged = hasSessionChanges(form, status);
  const authenticationChanged = hasAuthenticationChanges(form, status);
  const showSession = section !== "authentication";
  const showAuthentication = section !== "session";

  return (
    <div className="grid gap-5">
      <div className="flex items-start justify-between gap-4 rounded-lg border bg-muted/30 p-4">
        <div className="flex min-w-0 items-start gap-3">
          <ShieldCheck className="mt-0.5 size-5 shrink-0 text-primary" aria-hidden="true" />
          <div className="grid gap-1">
            <p className="text-sm font-medium">Secure runtime configuration</p>
            <p className="text-xs text-muted-foreground">{status.managementDescription}</p>
          </div>
        </div>
        <Badge className="shrink-0" variant={managementAvailable ? "secondary" : "outline"}>
          {managementAvailable ? "Management available" : "Read-only fallback"}
        </Badge>
      </div>

      {!managementAvailable ? (
        <div className="flex items-start gap-2 rounded-lg border border-primary/30 bg-muted/50 p-3 text-sm">
          <CircleAlert className="mt-0.5 size-4 shrink-0" aria-hidden="true" />
          <p>
            These values are read-only here because the configured management store is unavailable.
            Use the deployment configuration described above until backend management is available.
          </p>
        </div>
      ) : null}

      <StatusMessage message={message} />

      {showSession ? (
        <SessionConfigurationCard
          form={form}
          setForm={setForm}
          managementAvailable={managementAvailable}
          saving={saving}
          sessionChanged={sessionChanged}
          onSubmit={() =>
            save(
              makeSessionUpdate(form, status),
              "Enter session lifetimes from 15 minutes to 30 days.",
            )
          }
        />
      ) : null}

      {showAuthentication ? (
        <AuthenticationConfigurationCard
          form={form}
          status={status}
          setForm={setForm}
          managementAvailable={managementAvailable}
          saving={saving}
          authenticationChanged={authenticationChanged}
          onSubmit={() =>
            save(
              makeAuthenticationUpdate(form, status),
              "There are no authentication changes to save.",
            )
          }
        />
      ) : null}

      {showAuthentication ? (
        <div
          className={`flex items-start gap-2 rounded-lg border p-3 text-sm ${
            status.authentication.restartRequired
              ? "border-primary/30 bg-muted/50"
              : "border-border bg-muted/20"
          }`}
          role="status"
          aria-live="polite"
        >
          {status.authentication.restartRequired ? (
            <CircleAlert className="mt-0.5 size-4 shrink-0" aria-hidden="true" />
          ) : (
            <CheckCircle2 className="mt-0.5 size-4 shrink-0" aria-hidden="true" />
          )}
          <p>
            {status.authentication.restartRequired
              ? "A service restart is required before the latest authentication configuration is active."
              : "No authentication service restart is currently pending."}
          </p>
        </div>
      ) : null}
    </div>
  );
}

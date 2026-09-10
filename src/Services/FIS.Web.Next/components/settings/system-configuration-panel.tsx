"use client";

import * as React from "react";
import { CheckCircle2, CircleAlert, KeyRound, RefreshCw, Save, ShieldCheck } from "lucide-react";

import {
  getSystemConfigurationAction,
  saveSystemConfigurationAction,
} from "@/app/_actions/system-configuration";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import {
  Card,
  CardContent,
  CardDescription,
  CardFooter,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Switch } from "@/components/ui/switch";
import type {
  SystemConfigurationStatus,
  SystemConfigurationUpdate,
} from "@/lib/api/administration/api-system-configuration";

const MIN_REFRESH_LIFETIME_MINUTES = 15;
const MAX_REFRESH_LIFETIME_MINUTES = 30 * 24 * 60;

type FormState = {
  standardRefreshLifetimeMinutes: string;
  rememberRefreshLifetimeMinutes: string;
  entraEnabled: boolean;
  tenantId: string;
  clientId: string;
  clientSecret: string;
  passwordResetSigningKey: string;
};

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

function Field({
  id,
  label,
  hint,
  children,
}: Readonly<{ id: string; label: string; hint?: string; children: React.ReactNode }>) {
  return (
    <div className="grid gap-1.5">
      <Label className="text-xs" htmlFor={id}>
        {label}
      </Label>
      {children}
      {hint ? (
        <p className="text-xs text-muted-foreground" id={`${id}-hint`}>
          {hint}
        </p>
      ) : null}
    </div>
  );
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
        <form
          onSubmit={(event) => {
            event.preventDefault();
            save(
              makeSessionUpdate(form, status),
              "Enter session lifetimes from 15 minutes to 30 days.",
            );
          }}
        >
          <Card>
            <CardHeader>
              <CardTitle>Session duration</CardTitle>
              <CardDescription>
                Refresh lifetimes control how long signed-in users can renew access. Values are
                bounded to 15 minutes–30 days for predictable session safety.
              </CardDescription>
            </CardHeader>
            <CardContent className="grid gap-4 sm:grid-cols-2">
              <Field
                id="standard-refresh-lifetime"
                label="Standard refresh lifetime (minutes)"
                hint="Minimum 15 minutes; maximum 43,200 minutes (30 days)."
              >
                <Input
                  id="standard-refresh-lifetime"
                  type="number"
                  min={MIN_REFRESH_LIFETIME_MINUTES}
                  max={MAX_REFRESH_LIFETIME_MINUTES}
                  step="1"
                  inputMode="numeric"
                  value={form.standardRefreshLifetimeMinutes}
                  onChange={(event) =>
                    setForm({ ...form, standardRefreshLifetimeMinutes: event.target.value })
                  }
                  disabled={!managementAvailable || saving}
                  aria-describedby="standard-refresh-lifetime-hint"
                />
              </Field>
              <Field
                id="remember-refresh-lifetime"
                label="Remember-me refresh lifetime (minutes)"
                hint="Minimum 15 minutes; maximum 43,200 minutes (30 days)."
              >
                <Input
                  id="remember-refresh-lifetime"
                  type="number"
                  min={MIN_REFRESH_LIFETIME_MINUTES}
                  max={MAX_REFRESH_LIFETIME_MINUTES}
                  step="1"
                  inputMode="numeric"
                  value={form.rememberRefreshLifetimeMinutes}
                  onChange={(event) =>
                    setForm({ ...form, rememberRefreshLifetimeMinutes: event.target.value })
                  }
                  disabled={!managementAvailable || saving}
                  aria-describedby="remember-refresh-lifetime-hint"
                />
              </Field>
            </CardContent>
            <CardFooter className="justify-between gap-3 border-t pt-4">
              <p className="text-xs text-muted-foreground">
                {sessionChanged ? "Unsaved session changes" : "No session changes to save."}
              </p>
              <Button type="submit" disabled={!managementAvailable || !sessionChanged || saving}>
                <Save aria-hidden="true" />
                {saving ? "Saving…" : "Save session duration"}
              </Button>
            </CardFooter>
          </Card>
        </form>
      ) : null}

      {showAuthentication ? (
        <form
          onSubmit={(event) => {
            event.preventDefault();
            save(
              makeAuthenticationUpdate(form, status),
              "There are no authentication changes to save.",
            );
          }}
        >
          <Card>
            <CardHeader>
              <CardTitle>Authentication configuration</CardTitle>
              <CardDescription>
                Configure optional Entra sign-in metadata and write-only key material. Existing
                secrets are represented by status only and are never loaded into this form.
              </CardDescription>
            </CardHeader>
            <CardContent className="grid gap-5">
              <div className="flex items-start justify-between gap-4 rounded-lg border p-3">
                <div className="grid gap-1">
                  <Label className="text-sm" htmlFor="entra-enabled">
                    Enable Entra ID sign-in
                  </Label>
                  <p className="text-xs text-muted-foreground">
                    Keep disabled when this installation uses the existing FIS sign-in only.
                  </p>
                </div>
                <Switch
                  id="entra-enabled"
                  checked={form.entraEnabled}
                  onCheckedChange={(checked) => setForm({ ...form, entraEnabled: checked })}
                  disabled={!managementAvailable || saving}
                  aria-label="Enable Entra ID sign-in"
                />
              </div>

              <div className="grid gap-4 sm:grid-cols-2">
                <Field
                  id="entra-tenant-id"
                  label="Entra tenant ID"
                  hint="Enter a replacement only; configured values are never shown here."
                >
                  <Input
                    id="entra-tenant-id"
                    value={form.tenantId}
                    onChange={(event) => setForm({ ...form, tenantId: event.target.value })}
                    disabled={!managementAvailable || !form.entraEnabled || saving}
                    autoComplete="off"
                    aria-describedby="entra-tenant-id-hint"
                  />
                </Field>
                <Field
                  id="entra-client-id"
                  label="Entra client ID"
                  hint="Enter a replacement only; configured values are never shown here."
                >
                  <Input
                    id="entra-client-id"
                    value={form.clientId}
                    onChange={(event) => setForm({ ...form, clientId: event.target.value })}
                    disabled={!managementAvailable || !form.entraEnabled || saving}
                    autoComplete="off"
                    aria-describedby="entra-client-id-hint"
                  />
                </Field>
              </div>

              <div className="grid gap-3 rounded-lg border bg-muted/20 p-3">
                <div className="flex items-start gap-2">
                  <KeyRound className="mt-0.5 size-4 shrink-0 text-primary" aria-hidden="true" />
                  <div className="grid gap-1">
                    <p className="text-sm font-medium">Write-only secrets</p>
                    <p className="text-xs text-muted-foreground">
                      Enter a new value only when replacing a configured secret. Values are sent on
                      save and are never displayed afterward.
                    </p>
                  </div>
                </div>
                <div className="grid gap-4 sm:grid-cols-2">
                  <Field
                    id="entra-client-secret"
                    label="New Entra client secret"
                    hint={
                      status.authentication.clientSecretConfigured
                        ? "Configured; the current value is hidden."
                        : "Not configured; enter a value only if Entra sign-in is required."
                    }
                  >
                    <Input
                      id="entra-client-secret"
                      type="password"
                      autoComplete="new-password"
                      value={form.clientSecret}
                      onChange={(event) => setForm({ ...form, clientSecret: event.target.value })}
                      disabled={!managementAvailable || !form.entraEnabled || saving}
                      aria-describedby="entra-client-secret-hint"
                    />
                  </Field>
                  <Field
                    id="password-reset-signing-key"
                    label="New password-reset signing key"
                    hint={
                      status.authentication.passwordResetSigningKeyConfigured
                        ? "Configured; the current value is hidden."
                        : "Not configured; enter a value only if password reset signing is enabled."
                    }
                  >
                    <Input
                      id="password-reset-signing-key"
                      type="password"
                      autoComplete="new-password"
                      value={form.passwordResetSigningKey}
                      onChange={(event) =>
                        setForm({ ...form, passwordResetSigningKey: event.target.value })
                      }
                      disabled={!managementAvailable || saving}
                      aria-describedby="password-reset-signing-key-hint"
                    />
                  </Field>
                </div>
                <div
                  className="flex flex-wrap gap-2 text-xs"
                  aria-label="Secret configuration status"
                >
                  <Badge variant="outline">
                    Client secret:{" "}
                    {status.authentication.clientSecretConfigured ? "configured" : "not configured"}
                  </Badge>
                  <Badge variant="outline">
                    Password-reset key:{" "}
                    {status.authentication.passwordResetSigningKeyConfigured
                      ? "configured"
                      : "not configured"}
                  </Badge>
                </div>
              </div>
            </CardContent>
            <CardFooter className="justify-between gap-3 border-t pt-4">
              <p className="text-xs text-muted-foreground">
                {authenticationChanged
                  ? "Unsaved authentication changes"
                  : "No authentication changes to save."}
              </p>
              <Button
                type="submit"
                disabled={!managementAvailable || !authenticationChanged || saving}
              >
                <Save aria-hidden="true" />
                {saving ? "Saving…" : "Save authentication"}
              </Button>
            </CardFooter>
          </Card>
        </form>
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

"use client";

import * as React from "react";
import { KeyRound, Save } from "lucide-react";

import type { SystemConfigurationStatus } from "@/lib/api/administration/api-system-configuration";
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

export type SystemFormState = {
  standardRefreshLifetimeMinutes: string;
  rememberRefreshLifetimeMinutes: string;
  entraEnabled: boolean;
  tenantId: string;
  clientId: string;
  clientSecret: string;
  passwordResetSigningKey: string;
};

type FormSetter = React.Dispatch<React.SetStateAction<SystemFormState | null>>;

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

export function SessionConfigurationCard({
  form,
  setForm,
  managementAvailable,
  saving,
  sessionChanged,
  onSubmit,
}: Readonly<{
  form: SystemFormState;
  setForm: FormSetter;
  managementAvailable: boolean;
  saving: boolean;
  sessionChanged: boolean;
  onSubmit: () => void;
}>) {
  return (
    <form
      onSubmit={(event) => {
        event.preventDefault();
        onSubmit();
      }}
    >
      <Card>
        <CardHeader>
          <CardTitle>Session duration</CardTitle>
          <CardDescription>
            Refresh lifetimes control how long signed-in users can renew access. Values are bounded
            to 15 minutes–30 days for predictable session safety.
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
              min={15}
              max={30 * 24 * 60}
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
              min={15}
              max={30 * 24 * 60}
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
  );
}

export function AuthenticationConfigurationCard({
  form,
  status,
  setForm,
  managementAvailable,
  saving,
  authenticationChanged,
  onSubmit,
}: Readonly<{
  form: SystemFormState;
  status: SystemConfigurationStatus;
  setForm: FormSetter;
  managementAvailable: boolean;
  saving: boolean;
  authenticationChanged: boolean;
  onSubmit: () => void;
}>) {
  return (
    <form
      onSubmit={(event) => {
        event.preventDefault();
        onSubmit();
      }}
    >
      <Card>
        <CardHeader>
          <CardTitle>Authentication configuration</CardTitle>
          <CardDescription>
            Configure optional Entra sign-in metadata and write-only key material. Existing secrets
            are represented by status only and are never loaded into this form.
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
                  Enter a new value only when replacing a configured secret. Values are sent on save
                  and are never displayed afterward.
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
            <div className="flex flex-wrap gap-2 text-xs" aria-label="Secret configuration status">
              <BadgeStatus
                label="Client secret"
                configured={status.authentication.clientSecretConfigured}
              />
              <BadgeStatus
                label="Password-reset key"
                configured={status.authentication.passwordResetSigningKeyConfigured}
              />
            </div>
          </div>
        </CardContent>
        <CardFooter className="justify-between gap-3 border-t pt-4">
          <p className="text-xs text-muted-foreground">
            {authenticationChanged
              ? "Unsaved authentication changes"
              : "No authentication changes to save."}
          </p>
          <Button type="submit" disabled={!managementAvailable || !authenticationChanged || saving}>
            <Save aria-hidden="true" />
            {saving ? "Saving…" : "Save authentication"}
          </Button>
        </CardFooter>
      </Card>
    </form>
  );
}

function BadgeStatus({ label, configured }: Readonly<{ label: string; configured: boolean }>) {
  return (
    <Badge variant="outline">
      {label}: {configured ? "configured" : "not configured"}
    </Badge>
  );
}

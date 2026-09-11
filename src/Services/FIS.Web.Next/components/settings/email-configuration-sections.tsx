"use client";

import * as React from "react";
import { ChevronDown, MailCheck } from "lucide-react";

import type {
  EmailConfigurationStatus,
  EmailProvider,
  EmailProviderStatus,
} from "@/lib/api/administration/api-email-configuration";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { Collapsible, CollapsibleContent, CollapsibleTrigger } from "@/components/ui/collapsible";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Separator } from "@/components/ui/separator";

const PROVIDERS: EmailProvider[] = ["Graph", "Smtp", "SendGrid"];

export type EmailFormState = {
  providerOrder: EmailProvider[];
  timeoutSeconds: string;
  maxTotalAttachmentMiB: string;
  graph: {
    endpoint: string;
    authentication: "ManagedIdentity" | "ClientSecret";
    tenantId: string;
    clientId: string;
    managedIdentityClientId: string;
    senderUserPrincipalName: string;
    fromAddress: string;
    fromName: string;
    clientSecret: string;
  };
  smtp: {
    host: string;
    port: string;
    securityMode: "StartTls" | "SslOnConnect";
    authentication: "None" | "Password" | "GoogleOAuth2";
    username: string;
    password: string;
    googleOAuthClientId: string;
    googleOAuthClientSecret: string;
    googleOAuthRefreshToken: string;
    fromAddress: string;
    fromName: string;
  };
  sendGrid: { fromEmail: string; fromName: string; apiKey: string };
};

type FormSetter = React.Dispatch<React.SetStateAction<EmailFormState | null>>;

function statusBadge(status: "healthy" | "warning" | "neutral") {
  return status === "healthy" ? "default" : status === "warning" ? "secondary" : "outline";
}

function providerLabel(provider: EmailProvider) {
  return provider === "Graph" ? "Microsoft Graph" : provider === "Smtp" ? "SMTP" : "SendGrid";
}

function Field({
  label,
  hint,
  children,
}: Readonly<{ label: string; hint?: string; children: React.ReactNode }>) {
  const controlId = React.isValidElement<{ id?: string }>(children) ? children.props.id : undefined;
  return (
    <div className="grid gap-1.5">
      <Label className="text-xs" htmlFor={controlId}>
        {label}
      </Label>
      {children}
      {hint ? <p className="text-xs text-muted-foreground">{hint}</p> : null}
    </div>
  );
}

function SelectField({
  label,
  value,
  onChange,
  options,
  disabled = false,
}: Readonly<{
  label: string;
  value: string;
  onChange: (value: string) => void;
  options: ReadonlyArray<{ value: string; label: string }>;
  disabled?: boolean;
}>) {
  const id = label.replaceAll(" ", "-").toLowerCase();
  return (
    <div className="grid gap-1.5">
      <Label className="text-xs" htmlFor={id}>
        {label}
      </Label>
      <select
        id={id}
        className="h-9 w-full rounded-md border border-input bg-background px-3 text-sm shadow-sm focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
        value={value}
        onChange={(event) => onChange(event.target.value)}
        disabled={disabled}
      >
        {options.map((option) => (
          <option key={option.value} value={option.value}>
            {option.label}
          </option>
        ))}
      </select>
    </div>
  );
}

function ProviderSection({
  title,
  description,
  status,
  children,
  defaultOpen = false,
}: Readonly<{
  title: string;
  description: string;
  status?: EmailProviderStatus;
  children: React.ReactNode;
  defaultOpen?: boolean;
}>) {
  const [open, setOpen] = React.useState(defaultOpen);
  const state = status?.available ? "healthy" : status?.configured ? "warning" : "neutral";
  return (
    <Collapsible open={open} onOpenChange={setOpen} className="rounded-lg border bg-card">
      <CollapsibleTrigger className="flex w-full items-center justify-between gap-3 p-4 text-left hover:bg-muted/40 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring">
        <span className="grid gap-1">
          <span className="font-medium">{title}</span>
          <span className="text-xs font-normal text-muted-foreground">{description}</span>
        </span>
        <span className="flex shrink-0 items-center gap-2">
          <Badge variant={statusBadge(state)}>
            {status?.requiresActivation ? "Test required" : (status?.health ?? "Not configured")}
          </Badge>
          <ChevronDown
            className={`size-4 transition-transform ${open ? "rotate-180" : ""}`}
            aria-hidden="true"
          />
        </span>
      </CollapsibleTrigger>
      <CollapsibleContent>
        <Separator />
        <div className="grid gap-4 p-4">{children}</div>
      </CollapsibleContent>
    </Collapsible>
  );
}

function updateProviderOrder(setForm: FormSetter, provider: EmailProvider, checked: boolean) {
  setForm((current) =>
    current
      ? {
          ...current,
          providerOrder: checked
            ? [...current.providerOrder, provider]
            : current.providerOrder.filter((item) => item !== provider),
        }
      : current,
  );
}

function moveProvider(setForm: FormSetter, provider: EmailProvider, direction: -1 | 1) {
  setForm((current) => {
    if (!current) return current;
    const index = current.providerOrder.indexOf(provider);
    const destination = index + direction;
    if (index < 0 || destination < 0 || destination >= current.providerOrder.length) {
      return current;
    }
    const order = [...current.providerOrder];
    [order[index], order[destination]] = [order[destination], order[index]];
    return { ...current, providerOrder: order };
  });
}

function ProviderOrderRow({
  provider,
  enabled,
  position,
  providerCount,
  managementAvailable,
  onToggle,
  onMove,
}: Readonly<{
  provider: EmailProvider;
  enabled: boolean;
  position: number;
  providerCount: number;
  managementAvailable: boolean;
  onToggle: (checked: boolean) => void;
  onMove: (direction: -1 | 1) => void;
}>) {
  return (
    <div className="flex items-center justify-between gap-3 rounded-md border px-3 py-2">
      <div className="flex min-w-0 items-center gap-2">
        <Checkbox
          id={`email-provider-${provider}`}
          checked={enabled}
          onCheckedChange={(checked) => onToggle(checked === true)}
          disabled={!managementAvailable}
        />
        <Label
          className="cursor-pointer text-sm font-medium"
          htmlFor={`email-provider-${provider}`}
        >
          {providerLabel(provider)}
        </Label>
        {enabled && position === 0 ? <Badge variant="secondary">Primary</Badge> : null}
      </div>
      <div className="flex gap-1">
        <Button
          size="sm"
          variant="ghost"
          disabled={!enabled || position === 0 || !managementAvailable}
          onClick={() => onMove(-1)}
          aria-label={`Move ${providerLabel(provider)} earlier`}
        >
          ↑
        </Button>
        <Button
          size="sm"
          variant="ghost"
          disabled={!enabled || position === providerCount - 1 || !managementAvailable}
          onClick={() => onMove(1)}
          aria-label={`Move ${providerLabel(provider)} later`}
        >
          ↓
        </Button>
      </div>
    </div>
  );
}

export function EmailDeliverySettings({
  form,
  status,
  setForm,
}: Readonly<{
  form: EmailFormState;
  status: EmailConfigurationStatus;
  setForm: FormSetter;
}>) {
  const managementAvailable = status.configurationManagementAvailable;

  return (
    <div className="grid gap-3 rounded-lg border p-4">
      <div>
        <p className="font-medium">Delivery order</p>
        <p className="text-xs text-muted-foreground">
          The first enabled provider is primary. Fallback is used only before a message is accepted.
          A changed provider must pass a controlled test before it can send.
        </p>
      </div>
      <div className="grid gap-2">
        {PROVIDERS.map((provider) => {
          const enabled = form.providerOrder.includes(provider);
          const position = form.providerOrder.indexOf(provider);
          return (
            <ProviderOrderRow
              key={provider}
              provider={provider}
              enabled={enabled}
              position={position}
              providerCount={form.providerOrder.length}
              managementAvailable={managementAvailable}
              onToggle={(checked) => updateProviderOrder(setForm, provider, checked)}
              onMove={(direction) => moveProvider(setForm, provider, direction)}
            />
          );
        })}
      </div>
      <div className="grid grid-cols-2 gap-3">
        <Field label="Delivery timeout (seconds)">
          <Input
            id="delivery-timeout-seconds"
            type="number"
            min="5"
            max="120"
            inputMode="numeric"
            value={form.timeoutSeconds}
            onChange={(event) => setForm({ ...form, timeoutSeconds: event.target.value })}
            disabled={!managementAvailable}
          />
        </Field>
        <Field label="Attachment limit (MiB)">
          <Input
            id="attachment-limit-mib"
            type="number"
            min="1"
            max="20"
            inputMode="numeric"
            value={form.maxTotalAttachmentMiB}
            onChange={(event) => setForm({ ...form, maxTotalAttachmentMiB: event.target.value })}
            disabled={!managementAvailable}
          />
        </Field>
      </div>
    </div>
  );
}

export function EmailGraphProviderSettings({
  form,
  status,
  setForm,
}: Readonly<{
  form: EmailFormState;
  status: EmailConfigurationStatus;
  setForm: FormSetter;
}>) {
  const managementAvailable = status.configurationManagementAvailable;
  return (
    <ProviderSection
      title="Microsoft Graph"
      description="Preferred for Microsoft 365 and Entra-managed mailboxes."
      status={status.providers.find((item) => item.provider === "Graph")}
      defaultOpen
    >
      <SelectField
        label="Graph authentication"
        value={form.graph.authentication}
        onChange={(value) =>
          setForm({
            ...form,
            graph: {
              ...form.graph,
              authentication: value as EmailFormState["graph"]["authentication"],
            },
          })
        }
        disabled={!managementAvailable}
        options={[
          { value: "ManagedIdentity", label: "Managed identity (recommended)" },
          { value: "ClientSecret", label: "Client secret" },
        ]}
      />
      <div className="grid gap-3 sm:grid-cols-2">
        <Field label="Sender mailbox">
          <Input
            id="sender-mailbox"
            maxLength={320}
            value={form.graph.senderUserPrincipalName}
            onChange={(event) =>
              setForm({
                ...form,
                graph: { ...form.graph, senderUserPrincipalName: event.target.value },
              })
            }
            disabled={!managementAvailable}
          />
        </Field>
        <Field label="From name">
          <Input
            id="from-name"
            maxLength={120}
            value={form.graph.fromName}
            onChange={(event) =>
              setForm({ ...form, graph: { ...form.graph, fromName: event.target.value } })
            }
            disabled={!managementAvailable}
          />
        </Field>
        <Field label="Tenant ID">
          <Input
            id="tenant-id"
            maxLength={200}
            value={form.graph.tenantId}
            onChange={(event) =>
              setForm({ ...form, graph: { ...form.graph, tenantId: event.target.value } })
            }
            disabled={!managementAvailable || form.graph.authentication !== "ClientSecret"}
          />
        </Field>
        <Field label="Client ID">
          <Input
            id="client-id"
            maxLength={200}
            value={form.graph.clientId}
            onChange={(event) =>
              setForm({ ...form, graph: { ...form.graph, clientId: event.target.value } })
            }
            disabled={!managementAvailable || form.graph.authentication !== "ClientSecret"}
          />
        </Field>
        <Field
          label="Managed identity client ID"
          hint="Leave blank for the system-assigned identity."
        >
          <Input
            id="managed-identity-client-id"
            maxLength={200}
            value={form.graph.managedIdentityClientId}
            onChange={(event) =>
              setForm({
                ...form,
                graph: { ...form.graph, managedIdentityClientId: event.target.value },
              })
            }
            disabled={!managementAvailable || form.graph.authentication !== "ManagedIdentity"}
          />
        </Field>
        <Field
          label="New client secret"
          hint={
            status.graph.clientSecretConfigured
              ? "A secret is stored. Leave blank to keep it."
              : "Write-only; never displayed after saving."
          }
        >
          <Input
            id="graph-client-secret"
            type="password"
            autoComplete="new-password"
            maxLength={4096}
            value={form.graph.clientSecret}
            onChange={(event) =>
              setForm({ ...form, graph: { ...form.graph, clientSecret: event.target.value } })
            }
            disabled={!managementAvailable || form.graph.authentication !== "ClientSecret"}
          />
        </Field>
      </div>
    </ProviderSection>
  );
}

export function EmailSmtpProviderSettings({
  form,
  status,
  setForm,
}: Readonly<{
  form: EmailFormState;
  status: EmailConfigurationStatus;
  setForm: FormSetter;
}>) {
  const managementAvailable = status.configurationManagementAvailable;
  return (
    <ProviderSection
      title="SMTP"
      description="TLS-only SMTP for an approved domain relay, password mailbox, or Google OAuth mailbox."
      status={status.providers.find((item) => item.provider === "Smtp")}
    >
      <div className="grid gap-3 sm:grid-cols-2">
        <Field label="SMTP host">
          <Input
            id="smtp-host"
            maxLength={253}
            value={form.smtp.host}
            onChange={(event) =>
              setForm({ ...form, smtp: { ...form.smtp, host: event.target.value } })
            }
            disabled={!managementAvailable}
          />
        </Field>
        <Field label="Port">
          <Input
            id="smtp-port"
            type="number"
            min="465"
            max="587"
            inputMode="numeric"
            value={form.smtp.port}
            onChange={(event) =>
              setForm({ ...form, smtp: { ...form.smtp, port: event.target.value } })
            }
            disabled={!managementAvailable}
          />
        </Field>
        <SelectField
          label="TLS mode"
          value={form.smtp.securityMode}
          onChange={(value) =>
            setForm({
              ...form,
              smtp: {
                ...form.smtp,
                securityMode: value as EmailFormState["smtp"]["securityMode"],
              },
            })
          }
          options={[
            { value: "StartTls", label: "STARTTLS" },
            { value: "SslOnConnect", label: "SSL on connect" },
          ]}
          disabled={!managementAvailable}
        />
        <SelectField
          label="SMTP authentication"
          value={form.smtp.authentication}
          onChange={(value) =>
            setForm({
              ...form,
              smtp: {
                ...form.smtp,
                authentication: value as EmailFormState["smtp"]["authentication"],
              },
            })
          }
          options={[
            { value: "Password", label: "Username and password" },
            { value: "None", label: "Approved relay (no password)" },
            { value: "GoogleOAuth2", label: "Google OAuth 2.0 (Gmail only)" },
          ]}
          disabled={!managementAvailable}
        />
        <Field label="Username">
          <Input
            id="smtp-username"
            maxLength={320}
            value={form.smtp.username}
            onChange={(event) =>
              setForm({ ...form, smtp: { ...form.smtp, username: event.target.value } })
            }
            disabled={
              !managementAvailable ||
              (form.smtp.authentication !== "Password" &&
                form.smtp.authentication !== "GoogleOAuth2")
            }
          />
        </Field>
        <Field
          label="New SMTP password"
          hint={
            status.smtp.passwordConfigured
              ? "A password is stored. Leave blank to keep it."
              : "Write-only; never displayed after saving."
          }
        >
          <Input
            id="smtp-password"
            type="password"
            autoComplete="new-password"
            maxLength={4096}
            value={form.smtp.password}
            onChange={(event) =>
              setForm({ ...form, smtp: { ...form.smtp, password: event.target.value } })
            }
            disabled={!managementAvailable || form.smtp.authentication !== "Password"}
          />
        </Field>
        {form.smtp.authentication === "GoogleOAuth2" ? (
          <>
            <Field label="Google OAuth client ID">
              <Input
                id="google-oauth-client-id"
                maxLength={200}
                value={form.smtp.googleOAuthClientId}
                onChange={(event) =>
                  setForm({
                    ...form,
                    smtp: { ...form.smtp, googleOAuthClientId: event.target.value },
                  })
                }
                disabled={!managementAvailable}
              />
            </Field>
            <Field
              label="New Google OAuth client secret"
              hint={
                status.smtp.googleOAuthClientSecretConfigured
                  ? "A secret is stored. Leave blank to keep it."
                  : "Write-only; never displayed after saving."
              }
            >
              <Input
                id="google-oauth-client-secret"
                type="password"
                autoComplete="new-password"
                maxLength={4096}
                value={form.smtp.googleOAuthClientSecret}
                onChange={(event) =>
                  setForm({
                    ...form,
                    smtp: { ...form.smtp, googleOAuthClientSecret: event.target.value },
                  })
                }
                disabled={!managementAvailable}
              />
            </Field>
            <Field
              label="New Google OAuth refresh token"
              hint={
                status.smtp.googleOAuthRefreshTokenConfigured
                  ? "A refresh token is stored. Leave blank to keep it."
                  : "Create it with Gmail SMTP scope, then paste it once."
              }
            >
              <Input
                id="google-oauth-refresh-token"
                type="password"
                autoComplete="new-password"
                maxLength={4096}
                value={form.smtp.googleOAuthRefreshToken}
                onChange={(event) =>
                  setForm({
                    ...form,
                    smtp: { ...form.smtp, googleOAuthRefreshToken: event.target.value },
                  })
                }
                disabled={!managementAvailable}
              />
            </Field>
          </>
        ) : null}
        <Field label="From address">
          <Input
            id="smtp-from-address"
            type="email"
            maxLength={320}
            value={form.smtp.fromAddress}
            onChange={(event) =>
              setForm({ ...form, smtp: { ...form.smtp, fromAddress: event.target.value } })
            }
            disabled={!managementAvailable}
          />
        </Field>
        <Field label="From name">
          <Input
            id="smtp-from-name"
            maxLength={120}
            value={form.smtp.fromName}
            onChange={(event) =>
              setForm({ ...form, smtp: { ...form.smtp, fromName: event.target.value } })
            }
            disabled={!managementAvailable}
          />
        </Field>
      </div>
    </ProviderSection>
  );
}

export function EmailSendGridProviderSettings({
  form,
  status,
  setForm,
}: Readonly<{
  form: EmailFormState;
  status: EmailConfigurationStatus;
  setForm: FormSetter;
}>) {
  const managementAvailable = status.configurationManagementAvailable;
  return (
    <ProviderSection
      title="SendGrid"
      description="Optional API fallback for organisations that retain a SendGrid account."
      status={status.providers.find((item) => item.provider === "SendGrid")}
    >
      <div className="grid gap-3 sm:grid-cols-2">
        <Field label="From email">
          <Input
            id="sendgrid-from-email"
            type="email"
            maxLength={320}
            value={form.sendGrid.fromEmail}
            onChange={(event) =>
              setForm({ ...form, sendGrid: { ...form.sendGrid, fromEmail: event.target.value } })
            }
            disabled={!managementAvailable}
          />
        </Field>
        <Field label="From name">
          <Input
            id="sendgrid-from-name"
            maxLength={120}
            value={form.sendGrid.fromName}
            onChange={(event) =>
              setForm({ ...form, sendGrid: { ...form.sendGrid, fromName: event.target.value } })
            }
            disabled={!managementAvailable}
          />
        </Field>
        <Field
          label="New API key"
          hint={
            status.sendGrid.apiKeyConfigured
              ? "An API key is stored. Leave blank to keep it."
              : "Write-only; never displayed after saving."
          }
        >
          <Input
            id="sendgrid-api-key"
            type="password"
            autoComplete="new-password"
            maxLength={4096}
            value={form.sendGrid.apiKey}
            onChange={(event) =>
              setForm({ ...form, sendGrid: { ...form.sendGrid, apiKey: event.target.value } })
            }
            disabled={!managementAvailable}
          />
        </Field>
      </div>
    </ProviderSection>
  );
}

export function EmailProviderTestSettings({
  form,
  testRecipient,
  setTestRecipient,
  testing,
  saving,
  hasUnsavedChanges,
  onTest,
}: Readonly<{
  form: EmailFormState;
  testRecipient: string;
  setTestRecipient: (value: string) => void;
  testing: boolean;
  saving: boolean;
  hasUnsavedChanges: boolean;
  onTest: (provider: EmailProvider) => void;
}>) {
  return (
    <div className="grid gap-3 rounded-lg border p-4">
      <div className="flex items-start gap-3">
        <MailCheck className="mt-0.5 size-5 text-primary" />
        <div>
          <p className="font-medium">Provider test</p>
          <p className="text-xs text-muted-foreground">
            Sends one controlled message through the selected provider only. It never falls back;
            save configuration changes before testing. A successful test activates that exact secure
            configuration; a changed provider cannot send until it is tested again.
          </p>
        </div>
      </div>
      <div className="flex flex-col gap-2 sm:flex-row">
        <Input
          type="email"
          autoComplete="email"
          aria-label="Test recipient email"
          placeholder="administrator@example.gov.za"
          value={testRecipient}
          onChange={(event) => setTestRecipient(event.target.value)}
        />
        <div className="flex flex-wrap gap-2">
          {form.providerOrder.map((provider) => (
            <Button
              key={provider}
              type="button"
              size="sm"
              variant="outline"
              onClick={() => onTest(provider)}
              disabled={testing || saving || hasUnsavedChanges}
            >
              {testing
                ? "Testing…"
                : hasUnsavedChanges
                  ? "Save changes before testing"
                  : `Test ${providerLabel(provider)}`}
            </Button>
          ))}
        </div>
      </div>
    </div>
  );
}

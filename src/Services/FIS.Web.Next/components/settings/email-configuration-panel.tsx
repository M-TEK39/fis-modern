"use client";

import * as React from "react";
import {
  CheckCircle2,
  ChevronDown,
  CircleAlert,
  Cloud,
  MailCheck,
  ShieldCheck,
} from "lucide-react";

import {
  getEmailConfigurationAction,
  saveEmailConfigurationAction,
  testEmailProviderAction,
} from "@/app/_actions/email-configuration";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Collapsible, CollapsibleContent, CollapsibleTrigger } from "@/components/ui/collapsible";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Separator } from "@/components/ui/separator";
import { Checkbox } from "@/components/ui/checkbox";
import type {
  EmailConfigurationStatus,
  EmailConfigurationUpdate,
  EmailProvider,
} from "@/lib/api/administration/api-email-configuration";

const PROVIDERS: EmailProvider[] = ["Graph", "Smtp", "SendGrid"];

type FormState = {
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

function fromStatus(status: EmailConfigurationStatus): FormState {
  return {
    providerOrder: status.providerOrder,
    timeoutSeconds: String(status.timeoutSeconds),
    maxTotalAttachmentMiB: String(
      Math.max(1, Math.round(status.maxTotalAttachmentBytes / 1024 / 1024)),
    ),
    graph: {
      endpoint: status.graph.endpoint,
      authentication: status.graph.authentication,
      tenantId: status.graph.tenantId ?? "",
      clientId: status.graph.clientId ?? "",
      managedIdentityClientId: status.graph.managedIdentityClientId ?? "",
      senderUserPrincipalName: status.graph.senderUserPrincipalName ?? "",
      fromAddress: status.graph.fromAddress ?? "",
      fromName: status.graph.fromName ?? "",
      clientSecret: "",
    },
    smtp: {
      host: status.smtp.host ?? "",
      port: String(status.smtp.port),
      securityMode: status.smtp.securityMode,
      authentication: status.smtp.authentication,
      username: status.smtp.username ?? "",
      password: "",
      googleOAuthClientId: status.smtp.googleOAuthClientId ?? "",
      googleOAuthClientSecret: "",
      googleOAuthRefreshToken: "",
      fromAddress: status.smtp.fromAddress ?? "",
      fromName: status.smtp.fromName ?? "",
    },
    sendGrid: {
      fromEmail: status.sendGrid.fromEmail ?? "",
      fromName: status.sendGrid.fromName ?? "",
      apiKey: "",
    },
  };
}

function statusBadge(status: "healthy" | "warning" | "neutral") {
  return status === "healthy" ? "default" : status === "warning" ? "secondary" : "outline";
}

function providerLabel(provider: EmailProvider) {
  return provider === "Graph" ? "Microsoft Graph" : provider === "Smtp" ? "SMTP" : "SendGrid";
}

function text(value: string) {
  return value.trim();
}

function toSecret(value: string) {
  const trimmed = text(value);
  return trimmed || undefined;
}

function sameProviderOrder(left: EmailProvider[], right: EmailProvider[]) {
  return left.length === right.length && left.every((provider, index) => provider === right[index]);
}

function makeUpdate(
  form: FormState,
  current: EmailConfigurationStatus,
): EmailConfigurationUpdate | null {
  const timeoutSeconds = Number(form.timeoutSeconds);
  const maxTotalAttachmentMiB = Number(form.maxTotalAttachmentMiB);
  if (!Number.isInteger(timeoutSeconds) || timeoutSeconds < 5 || timeoutSeconds > 120) return null;
  if (
    !Number.isInteger(maxTotalAttachmentMiB) ||
    maxTotalAttachmentMiB < 1 ||
    maxTotalAttachmentMiB > 20
  )
    return null;

  const smtpPort = Number(form.smtp.port);
  if (!Number.isInteger(smtpPort) || (smtpPort !== 465 && smtpPort !== 587)) return null;

  const graph = {
    ...(text(form.graph.endpoint) !== current.graph.endpoint
      ? { endpoint: text(form.graph.endpoint) }
      : {}),
    ...(form.graph.authentication !== current.graph.authentication
      ? { authentication: form.graph.authentication }
      : {}),
    ...(text(form.graph.tenantId) !== (current.graph.tenantId ?? "")
      ? { tenantId: text(form.graph.tenantId) }
      : {}),
    ...(text(form.graph.clientId) !== (current.graph.clientId ?? "")
      ? { clientId: text(form.graph.clientId) }
      : {}),
    ...(text(form.graph.managedIdentityClientId) !== (current.graph.managedIdentityClientId ?? "")
      ? { managedIdentityClientId: text(form.graph.managedIdentityClientId) }
      : {}),
    ...(text(form.graph.senderUserPrincipalName) !== (current.graph.senderUserPrincipalName ?? "")
      ? { senderUserPrincipalName: text(form.graph.senderUserPrincipalName) }
      : {}),
    ...(text(form.graph.fromAddress) !== (current.graph.fromAddress ?? "")
      ? { fromAddress: text(form.graph.fromAddress) }
      : {}),
    ...(text(form.graph.fromName) !== (current.graph.fromName ?? "")
      ? { fromName: text(form.graph.fromName) }
      : {}),
    ...(toSecret(form.graph.clientSecret)
      ? { clientSecret: toSecret(form.graph.clientSecret) }
      : {}),
  };
  const smtp = {
    ...(text(form.smtp.host) !== (current.smtp.host ?? "") ? { host: text(form.smtp.host) } : {}),
    ...(smtpPort !== current.smtp.port ? { port: smtpPort } : {}),
    ...(form.smtp.securityMode !== current.smtp.securityMode
      ? { securityMode: form.smtp.securityMode }
      : {}),
    ...(form.smtp.authentication !== current.smtp.authentication
      ? { authentication: form.smtp.authentication }
      : {}),
    ...(text(form.smtp.username) !== (current.smtp.username ?? "")
      ? { username: text(form.smtp.username) }
      : {}),
    ...(text(form.smtp.fromAddress) !== (current.smtp.fromAddress ?? "")
      ? { fromAddress: text(form.smtp.fromAddress) }
      : {}),
    ...(text(form.smtp.fromName) !== (current.smtp.fromName ?? "")
      ? { fromName: text(form.smtp.fromName) }
      : {}),
    ...(toSecret(form.smtp.password) ? { password: toSecret(form.smtp.password) } : {}),
    ...(text(form.smtp.googleOAuthClientId) !== (current.smtp.googleOAuthClientId ?? "")
      ? { googleOAuthClientId: text(form.smtp.googleOAuthClientId) }
      : {}),
    ...(toSecret(form.smtp.googleOAuthClientSecret)
      ? { googleOAuthClientSecret: toSecret(form.smtp.googleOAuthClientSecret) }
      : {}),
    ...(toSecret(form.smtp.googleOAuthRefreshToken)
      ? { googleOAuthRefreshToken: toSecret(form.smtp.googleOAuthRefreshToken) }
      : {}),
  };
  const sendGrid = {
    ...(text(form.sendGrid.fromEmail) !== (current.sendGrid.fromEmail ?? "")
      ? { fromEmail: text(form.sendGrid.fromEmail) }
      : {}),
    ...(text(form.sendGrid.fromName) !== (current.sendGrid.fromName ?? "")
      ? { fromName: text(form.sendGrid.fromName) }
      : {}),
    ...(toSecret(form.sendGrid.apiKey) ? { apiKey: toSecret(form.sendGrid.apiKey) } : {}),
  };
  const update: EmailConfigurationUpdate = {
    ...(sameProviderOrder(form.providerOrder, current.providerOrder)
      ? {}
      : { providerOrder: form.providerOrder }),
    ...(timeoutSeconds === current.timeoutSeconds ? {} : { timeoutSeconds }),
    ...(maxTotalAttachmentMiB * 1024 * 1024 === current.maxTotalAttachmentBytes
      ? {}
      : { maxTotalAttachmentBytes: maxTotalAttachmentMiB * 1024 * 1024 }),
    ...(Object.keys(graph).length > 0 ? { graph } : {}),
    ...(Object.keys(smtp).length > 0 ? { smtp } : {}),
    ...(Object.keys(sendGrid).length > 0 ? { sendGrid } : {}),
  };
  return Object.keys(update).length > 0 ? update : {};
}

function hasFormChanges(form: FormState, current: EmailConfigurationStatus) {
  const saved = fromStatus(current);
  return (
    JSON.stringify({
      ...form,
      graph: { ...form.graph, clientSecret: Boolean(form.graph.clientSecret) },
      smtp: {
        ...form.smtp,
        password: Boolean(form.smtp.password),
        googleOAuthClientSecret: Boolean(form.smtp.googleOAuthClientSecret),
        googleOAuthRefreshToken: Boolean(form.smtp.googleOAuthRefreshToken),
      },
      sendGrid: { ...form.sendGrid, apiKey: Boolean(form.sendGrid.apiKey) },
    }) !==
    JSON.stringify({
      ...saved,
      graph: { ...saved.graph, clientSecret: false },
      smtp: {
        ...saved.smtp,
        password: false,
        googleOAuthClientSecret: false,
        googleOAuthRefreshToken: false,
      },
      sendGrid: { ...saved.sendGrid, apiKey: false },
    })
  );
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
  status?: { configured: boolean; available: boolean; health: string; requiresActivation: boolean };
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

export function EmailConfigurationPanel() {
  const [status, setStatus] = React.useState<EmailConfigurationStatus | null>(null);
  const [form, setForm] = React.useState<FormState | null>(null);
  const [loadError, setLoadError] = React.useState<string | null>(null);
  const [message, setMessage] = React.useState<{
    text: string;
    tone: "success" | "error";
  } | null>(null);
  const [testRecipient, setTestRecipient] = React.useState("");
  const [saving, startSave] = React.useTransition();
  const [testing, startTest] = React.useTransition();

  const refresh = React.useCallback(async () => {
    setLoadError(null);
    const result = await getEmailConfigurationAction();
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

  const hasUnsavedChanges = form && status ? hasFormChanges(form, status) : false;

  const providerStatus = (provider: EmailProvider) =>
    status?.providers.find((item) => item.provider === provider);
  const toggleProvider = (provider: EmailProvider, checked: boolean) => {
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
  };
  const moveProvider = (provider: EmailProvider, direction: -1 | 1) => {
    setForm((current) => {
      if (!current) return current;
      const index = current.providerOrder.indexOf(provider);
      const destination = index + direction;
      if (index < 0 || destination < 0 || destination >= current.providerOrder.length)
        return current;
      const order = [...current.providerOrder];
      [order[index], order[destination]] = [order[destination], order[index]];
      return { ...current, providerOrder: order };
    });
  };
  const save = () => {
    if (!form || !status) return;
    const update = makeUpdate(form, status);
    if (!update || update.providerOrder?.length === 0) {
      setMessage({
        text: "Choose at least one provider, a timeout from 5–120 seconds, an attachment limit from 1–20 MiB, and a valid SMTP port.",
        tone: "error",
      });
      return;
    }
    if (Object.keys(update).length === 0) {
      setMessage({ text: "There are no email configuration changes to save.", tone: "error" });
      return;
    }
    startSave(async () => {
      const result = await saveEmailConfigurationAction(update);
      setMessage({
        text:
          result.message ??
          (result.ok ? "Email configuration saved." : "Email configuration could not be saved."),
        tone: result.ok ? "success" : "error",
      });
      if (result.ok) await refresh();
    });
  };
  const test = (provider: EmailProvider) => {
    if (!testRecipient.trim()) {
      setMessage({ text: "Enter a test recipient before sending a provider test.", tone: "error" });
      return;
    }
    startTest(async () => {
      const result = await testEmailProviderAction(provider, testRecipient.trim());
      setMessage({
        text: result.message ?? (result.ok ? "Test message accepted." : "Provider test failed."),
        tone: result.ok ? "success" : "error",
      });
      if (result.ok) await refresh();
    });
  };

  if (loadError) {
    return (
      <div
        className="grid gap-3 rounded-lg border border-destructive/40 bg-destructive/5 p-4 text-sm"
        role="alert"
      >
        <div className="flex items-start gap-2">
          <CircleAlert className="mt-0.5 size-4" />
          <p>{loadError}</p>
        </div>
        <Button className="w-fit" size="sm" variant="outline" onClick={() => void refresh()}>
          Retry
        </Button>
      </div>
    );
  }
  if (!status || !form) {
    return (
      <div className="rounded-lg border border-dashed p-4 text-sm text-muted-foreground">
        Loading secure email configuration…
      </div>
    );
  }

  return (
    <div className="grid gap-5">
      <div className="rounded-lg border bg-muted/30 p-4">
        <div className="flex items-start gap-3">
          <ShieldCheck className="mt-0.5 size-5 text-primary" aria-hidden="true" />
          <div className="grid gap-1">
            <p className="text-sm font-medium">Secure runtime configuration</p>
            <p className="text-xs text-muted-foreground">{status.managementDescription}</p>
          </div>
        </div>
      </div>

      {!status.configurationManagementAvailable ? (
        <div className="rounded-lg border border-primary/30 bg-muted/50 p-3 text-sm text-foreground">
          Email delivery can use deployment configuration, but editing requires a reachable Azure
          Key Vault.
        </div>
      ) : null}

      <div className="grid gap-3 rounded-lg border p-4">
        <div>
          <p className="font-medium">Delivery order</p>
          <p className="text-xs text-muted-foreground">
            The first enabled provider is primary. Fallback is used only before a message is
            accepted. A changed provider must pass a controlled test before it can send.
          </p>
        </div>
        <div className="grid gap-2">
          {PROVIDERS.map((provider) => {
            const enabled = form.providerOrder.includes(provider);
            const position = form.providerOrder.indexOf(provider);
            return (
              <div
                key={provider}
                className="flex items-center justify-between gap-3 rounded-md border px-3 py-2"
              >
                <div className="flex min-w-0 items-center gap-2">
                  <Checkbox
                    id={`email-provider-${provider}`}
                    checked={enabled}
                    onCheckedChange={(checked) => toggleProvider(provider, checked === true)}
                    disabled={!status.configurationManagementAvailable}
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
                    disabled={
                      !enabled || position === 0 || !status.configurationManagementAvailable
                    }
                    onClick={() => moveProvider(provider, -1)}
                    aria-label={`Move ${providerLabel(provider)} earlier`}
                  >
                    ↑
                  </Button>
                  <Button
                    size="sm"
                    variant="ghost"
                    disabled={
                      !enabled ||
                      position === form.providerOrder.length - 1 ||
                      !status.configurationManagementAvailable
                    }
                    onClick={() => moveProvider(provider, 1)}
                    aria-label={`Move ${providerLabel(provider)} later`}
                  >
                    ↓
                  </Button>
                </div>
              </div>
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
              disabled={!status.configurationManagementAvailable}
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
              disabled={!status.configurationManagementAvailable}
            />
          </Field>
        </div>
      </div>

      <ProviderSection
        title="Microsoft Graph"
        description="Preferred for Microsoft 365 and Entra-managed mailboxes."
        status={providerStatus("Graph")}
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
                authentication: value as FormState["graph"]["authentication"],
              },
            })
          }
          disabled={!status.configurationManagementAvailable}
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
              disabled={!status.configurationManagementAvailable}
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
              disabled={!status.configurationManagementAvailable}
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
              disabled={
                !status.configurationManagementAvailable ||
                form.graph.authentication !== "ClientSecret"
              }
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
              disabled={
                !status.configurationManagementAvailable ||
                form.graph.authentication !== "ClientSecret"
              }
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
              disabled={
                !status.configurationManagementAvailable ||
                form.graph.authentication !== "ManagedIdentity"
              }
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
              disabled={
                !status.configurationManagementAvailable ||
                form.graph.authentication !== "ClientSecret"
              }
            />
          </Field>
        </div>
      </ProviderSection>

      <ProviderSection
        title="SMTP"
        description="TLS-only SMTP for an approved domain relay, password mailbox, or Google OAuth mailbox."
        status={providerStatus("Smtp")}
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
              disabled={!status.configurationManagementAvailable}
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
              disabled={!status.configurationManagementAvailable}
            />
          </Field>
          <SelectField
            label="TLS mode"
            value={form.smtp.securityMode}
            onChange={(value) =>
              setForm({
                ...form,
                smtp: { ...form.smtp, securityMode: value as FormState["smtp"]["securityMode"] },
              })
            }
            options={[
              { value: "StartTls", label: "STARTTLS" },
              { value: "SslOnConnect", label: "SSL on connect" },
            ]}
            disabled={!status.configurationManagementAvailable}
          />
          <SelectField
            label="SMTP authentication"
            value={form.smtp.authentication}
            onChange={(value) =>
              setForm({
                ...form,
                smtp: {
                  ...form.smtp,
                  authentication: value as FormState["smtp"]["authentication"],
                },
              })
            }
            options={[
              { value: "Password", label: "Username and password" },
              { value: "None", label: "Approved relay (no password)" },
              { value: "GoogleOAuth2", label: "Google OAuth 2.0 (Gmail only)" },
            ]}
            disabled={!status.configurationManagementAvailable}
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
                !status.configurationManagementAvailable ||
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
              disabled={
                !status.configurationManagementAvailable || form.smtp.authentication !== "Password"
              }
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
                  disabled={!status.configurationManagementAvailable}
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
                  disabled={!status.configurationManagementAvailable}
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
                  disabled={!status.configurationManagementAvailable}
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
              disabled={!status.configurationManagementAvailable}
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
              disabled={!status.configurationManagementAvailable}
            />
          </Field>
        </div>
      </ProviderSection>

      <ProviderSection
        title="SendGrid"
        description="Optional API fallback for organisations that retain a SendGrid account."
        status={providerStatus("SendGrid")}
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
              disabled={!status.configurationManagementAvailable}
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
              disabled={!status.configurationManagementAvailable}
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
              disabled={!status.configurationManagementAvailable}
            />
          </Field>
        </div>
      </ProviderSection>

      <div className="grid gap-3 rounded-lg border p-4">
        <div className="flex items-start gap-3">
          <MailCheck className="mt-0.5 size-5 text-primary" />
          <div>
            <p className="font-medium">Provider test</p>
            <p className="text-xs text-muted-foreground">
              Sends one controlled message through the selected provider only. It never falls back;
              save configuration changes before testing. A successful test activates that exact
              secure configuration; a changed provider cannot send until it is tested again.
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
                onClick={() => test(provider)}
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

      {message ? (
        <div
          className="flex items-start gap-2 rounded-lg border p-3 text-sm"
          role={message.tone === "error" ? "alert" : "status"}
        >
          {message.tone === "success" ? (
            <CheckCircle2 className="mt-0.5 size-4 text-primary" aria-hidden="true" />
          ) : (
            <CircleAlert className="mt-0.5 size-4 text-destructive" aria-hidden="true" />
          )}
          {message.text}
        </div>
      ) : null}
      <div className="flex flex-wrap items-center justify-between gap-3 border-t pt-4">
        <p className="flex items-center gap-2 text-xs text-muted-foreground">
          <Cloud className="size-3.5" />
          No secret is shown after saving.
        </p>
        <Button
          type="button"
          onClick={save}
          disabled={saving || !status.configurationManagementAvailable}
        >
          {saving ? "Saving securely…" : "Save email configuration"}
        </Button>
      </div>
    </div>
  );
}

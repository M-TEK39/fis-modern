"use client";

import * as React from "react";
import { CheckCircle2, CircleAlert, Cloud, ShieldCheck } from "lucide-react";

import {
  getEmailConfigurationAction,
  saveEmailConfigurationAction,
  testEmailProviderAction,
} from "@/app/_actions/email-configuration";
import { Button } from "@/components/ui/button";
import {
  EmailDeliverySettings,
  EmailGraphProviderSettings,
  EmailProviderTestSettings,
  EmailSendGridProviderSettings,
  EmailSmtpProviderSettings,
  type EmailFormState,
} from "@/components/settings/email-configuration-sections";
import type {
  EmailConfigurationStatus,
  EmailConfigurationUpdate,
  EmailProvider,
} from "@/lib/api/administration/api-email-configuration";

type FormState = EmailFormState;

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

      <EmailDeliverySettings form={form} status={status} setForm={setForm} />

      <EmailGraphProviderSettings form={form} status={status} setForm={setForm} />

      <EmailSmtpProviderSettings form={form} status={status} setForm={setForm} />

      <EmailSendGridProviderSettings form={form} status={status} setForm={setForm} />

      <EmailProviderTestSettings
        form={form}
        testRecipient={testRecipient}
        setTestRecipient={setTestRecipient}
        testing={testing}
        saving={saving}
        hasUnsavedChanges={hasUnsavedChanges}
        onTest={test}
      />

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

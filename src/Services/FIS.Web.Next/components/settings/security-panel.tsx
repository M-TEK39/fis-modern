"use client";

import Link from "next/link";
import { KeyRound, LockKeyhole, ShieldCheck } from "lucide-react";

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";

/**
 * FIS deliberately does not claim local MFA enrollment until an approved,
 * durable security store exists for authenticator secrets, passkey credentials,
 * replay prevention, and recovery controls. Entra users continue to follow
 * their tenant's own conditional-access policy.
 */
export function SecurityPanel({ onNavigate }: Readonly<{ onNavigate: () => void }>) {
  return (
    <div className="grid gap-4">
      <Card>
        <CardHeader>
          <div className="flex items-start gap-3">
            <ShieldCheck className="mt-0.5 size-5 text-primary" aria-hidden="true" />
            <div className="grid gap-1">
              <CardTitle>Multi-factor authentication</CardTitle>
              <CardDescription>
                Choose an identity provider that already enforces the assurance level required by
                your organisation.
              </CardDescription>
            </div>
          </div>
        </CardHeader>
        <CardContent className="grid gap-3 text-sm">
          <div className="flex flex-wrap gap-2" aria-label="Local MFA capability status">
            <Badge variant="outline">Authenticator app: pending approved security store</Badge>
            <Badge variant="outline">Email OTP: pending approved security store</Badge>
            <Badge variant="outline">Passkeys: pending approved security store</Badge>
          </div>
          <p className="text-muted-foreground">
            Local MFA is not forced or simulated. FIS will expose enrollment only after an approved
            durable store and recovery process are available. Microsoft Entra sign-in continues to
            honour the tenant&apos;s MFA and Conditional Access policies.
          </p>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <div className="flex items-start gap-3">
            <LockKeyhole className="mt-0.5 size-5 text-primary" aria-hidden="true" />
            <div className="grid gap-1">
              <CardTitle>Password and account recovery</CardTitle>
              <CardDescription>
                Change your password or update your existing recovery question using the secure FIS
                authentication flow.
              </CardDescription>
            </div>
          </div>
        </CardHeader>
        <CardContent className="flex flex-wrap gap-2">
          <Button asChild>
            <Link href="/change-password" onClick={onNavigate}>
              <KeyRound aria-hidden="true" />
              Change password
            </Link>
          </Button>
          <Button asChild variant="outline">
            <Link href="/change-password-question" onClick={onNavigate}>
              Update password and question
            </Link>
          </Button>
        </CardContent>
      </Card>
    </div>
  );
}

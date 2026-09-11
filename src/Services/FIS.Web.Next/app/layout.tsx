import type { Metadata } from "next";
import { connection } from "next/server";
import { Suspense, type ReactNode } from "react";

import { logoutAction } from "@/app/(auth)/actions/auth";
import { AppShellFrame, getAppShellData } from "@/components/app-shell/app-shell";
import SessionKeepAlive from "@/components/app-shell/session-keep-alive";
import SiteHeader from "@/components/app-shell/site-header";
import { AppSidebar16 } from "@/components/ui/sidebar/app-sidebar-16";
import { ThemeProvider } from "@/components/theme-provider";
import { getSession } from "@/lib/auth/session";

import "./globals.css";

export const metadata: Metadata = {
  title: {
    default: "Fleet Information System",
    template: "%s | Fleet Information System",
  },
  description: "Fleet Information System",
};

async function AuthenticatedHeader() {
  await connection();
  const session = await getSession();

  if (session.status !== "authenticated") {
    return null;
  }

  return (
    <>
      <SessionKeepAlive />
      <SiteHeader groups={getAppShellData(session).groups} />
    </>
  );
}

async function AuthenticatedSidebar() {
  await connection();
  const session = await getSession();

  if (session.status !== "authenticated") {
    return null;
  }

  const { groups, user, canManageSystemSettings } = getAppShellData(session);
  return (
    <AppSidebar16
      groups={groups}
      user={user}
      canManageSystemSettings={canManageSystemSettings}
      logoutAction={logoutAction}
    />
  );
}

export default function RootLayout({ children }: Readonly<{ children: ReactNode }>) {
  return (
    <html lang="en" suppressHydrationWarning>
      <body>
        <ThemeProvider>
          <AppShellFrame
            header={
              <Suspense fallback={null}>
                <AuthenticatedHeader />
              </Suspense>
            }
            sidebar={
              <Suspense fallback={null}>
                <AuthenticatedSidebar />
              </Suspense>
            }
          >
            {children}
          </AppShellFrame>
        </ThemeProvider>
      </body>
    </html>
  );
}

import type { Metadata } from "next";
import { connection } from "next/server";
import { Suspense, type ReactNode } from "react";

import { logoutAction } from "@/app/actions/auth";
import { AppShellFrame, getAppShellData } from "@/app/_components/app-shell";
import SiteHeader from "@/app/_components/site-header";
import { AppSidebar16 } from "@/components/ui/sidebar/app-sidebar-16";
import { ThemeProvider } from "@/components/theme-provider";
import { getSession } from "@/lib/session";

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

  return <SiteHeader groups={getAppShellData(session).groups} />;
}

async function AuthenticatedSidebar() {
  await connection();
  const session = await getSession();

  if (session.status !== "authenticated") {
    return null;
  }

  const { groups, user } = getAppShellData(session);
  return <AppSidebar16 groups={groups} user={user} logoutAction={logoutAction} />;
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

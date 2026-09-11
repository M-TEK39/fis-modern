"use client";

import * as React from "react";
import NextLink from "next/link";
import {
  CircleHelp,
  Clock3,
  KeyRound,
  Lock,
  Mail,
  Paintbrush,
  type LucideIcon,
} from "lucide-react";
import { useTheme } from "next-themes";

import {
  Breadcrumb,
  BreadcrumbItem,
  BreadcrumbList,
  BreadcrumbPage,
  BreadcrumbSeparator,
} from "../breadcrumb";
import { Button } from "../button";
import { EmailConfigurationPanel } from "@/components/settings/email-configuration-panel";
import { SecurityPanel } from "@/components/settings/security-panel";
import { SessionManagementPanel } from "@/components/settings/session-management-panel";
import { SystemConfigurationPanel } from "@/components/settings/system-configuration-panel";
import { ScrollArea } from "@/components/ui/scroll-area";
import { Dialog, DialogContent, DialogDescription, DialogTitle } from "../dialog";

type SettingsItem = {
  name: string;
  icon: LucideIcon;
  description: string;
  href?: string;
};

const subscribeToHydration = () => () => {};
const getClientHydrationSnapshot = () => true;
const getServerHydrationSnapshot = () => false;

const data: { nav: readonly SettingsItem[] } = {
  nav: [
    {
      name: "Appearance",
      icon: Paintbrush,
      description: "Choose how the workspace looks on this device.",
    },
    {
      name: "Security",
      icon: Lock,
      description: "Review password, account recovery, and MFA capability status.",
    },
    {
      name: "Help & manuals",
      icon: CircleHelp,
      description: "Find guidance for your fleet workflows.",
      href: "/manuals",
    },
  ],
};

function AppearanceSettings({
  mounted,
  theme,
  setTheme,
}: Readonly<{ mounted: boolean; theme?: string; setTheme: (theme: string) => void }>) {
  return (
    <div className="space-y-3">
      <p className="text-sm text-muted-foreground">
        Use a light or dark theme, or follow your device settings.
      </p>
      <div className="flex flex-wrap gap-2" aria-label="Theme preference">
        {(["light", "dark", "system"] as const).map((option) => (
          <Button
            key={option}
            type="button"
            variant={mounted && theme === option ? "default" : "outline"}
            aria-pressed={mounted && theme === option}
            onClick={() => setTheme(option)}
          >
            {option[0].toUpperCase() + option.slice(1)}
          </Button>
        ))}
      </div>
    </div>
  );
}

function SettingsSectionContent({
  activeItem,
  mounted,
  theme,
  setTheme,
  onClose,
}: Readonly<{
  activeItem: SettingsItem;
  mounted: boolean;
  theme?: string;
  setTheme: (theme: string) => void;
  onClose: () => void;
}>) {
  if (activeItem.href) {
    return (
      <Button asChild>
        <NextLink href={activeItem.href} onClick={onClose}>
          Open manuals
        </NextLink>
      </Button>
    );
  }

  if (activeItem.name === "Appearance") {
    return <AppearanceSettings mounted={mounted} theme={theme} setTheme={setTheme} />;
  }

  if (activeItem.name === "Email configuration") {
    return <EmailConfigurationPanel />;
  }

  if (activeItem.name === "Session management") {
    return (
      <div className="grid gap-5">
        <SystemConfigurationPanel section="session" />
        <SessionManagementPanel />
      </div>
    );
  }

  if (activeItem.name === "Authentication configuration") {
    return <SystemConfigurationPanel section="authentication" />;
  }

  if (activeItem.name === "Security") {
    return <SecurityPanel onNavigate={onClose} />;
  }

  return null;
}

export function SettingsDialog({
  open,
  onOpenChange,
  onCloseAutoFocus,
  canManageSystemSettings = false,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onCloseAutoFocus?: (event: Event) => void;
  canManageSystemSettings?: boolean;
}) {
  const [section, setSection] = React.useState("Appearance");
  const mounted = React.useSyncExternalStore(
    subscribeToHydration,
    getClientHydrationSnapshot,
    getServerHydrationSnapshot,
  );
  const { setTheme, theme } = useTheme();
  const navigation = React.useMemo(
    () => [
      ...data.nav,
      ...(canManageSystemSettings
        ? [
            {
              name: "Email configuration",
              icon: Mail,
              description: "Configure secure email delivery and provider fallback.",
            },
            {
              name: "Session management",
              icon: Clock3,
              description: "Set refresh lifetimes and revoke durable compromised sessions.",
            },
            {
              name: "Authentication configuration",
              icon: KeyRound,
              description: "Configure optional Entra sign-in and rotate reset signing material.",
            },
          ]
        : []),
    ],
    [canManageSystemSettings],
  );
  const activeItem = navigation.find((item) => item.name === section) ?? navigation[0];

  const handleOpenChange = (nextOpen: boolean) => {
    if (!nextOpen) setSection("Appearance");
    onOpenChange(nextOpen);
  };

  return (
    <Dialog open={open} onOpenChange={handleOpenChange}>
      <DialogContent
        onCloseAutoFocus={onCloseAutoFocus}
        className="flex h-[min(90svh,32rem)] flex-col gap-0 overflow-hidden p-0 sm:max-w-[700px] lg:max-w-[800px]"
      >
        <DialogTitle className="sr-only">FIS settings</DialogTitle>
        <DialogDescription className="sr-only">
          Review available FIS account, navigation, security, and help options.
        </DialogDescription>
        <div className="flex min-h-0 flex-1">
          <aside className="hidden h-full w-60 shrink-0 flex-col border-r bg-muted/20 md:flex">
            <nav className="grid gap-1 p-2" aria-label="Settings sections">
              {navigation.map((item) => {
                const isActive = item.name === activeItem.name;
                return (
                  <button
                    key={item.name}
                    type="button"
                    className={`flex min-h-9 items-center gap-2 rounded-md px-3 py-2 text-left text-sm font-medium transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring ${
                      isActive
                        ? "bg-sidebar-accent text-sidebar-accent-foreground"
                        : "text-muted-foreground hover:bg-accent hover:text-accent-foreground"
                    }`}
                    aria-current={isActive ? "page" : undefined}
                    onClick={() => setSection(item.name)}
                  >
                    <item.icon className="size-4 shrink-0" aria-hidden="true" />
                    <span>{item.name}</span>
                  </button>
                );
              })}
            </nav>
          </aside>
          <main className="flex min-h-0 flex-1 flex-col overflow-hidden">
            <header className="flex h-16 shrink-0 items-center gap-2">
              <div className="flex items-center gap-2 px-4">
                <Breadcrumb>
                  <BreadcrumbList>
                    <BreadcrumbItem className="hidden md:block">
                      <BreadcrumbPage>Settings</BreadcrumbPage>
                    </BreadcrumbItem>
                    <BreadcrumbSeparator className="hidden md:block" />
                    <BreadcrumbItem>
                      <BreadcrumbPage>{activeItem.name}</BreadcrumbPage>
                    </BreadcrumbItem>
                  </BreadcrumbList>
                </Breadcrumb>
              </div>
            </header>
            <div className="flex gap-1 overflow-x-auto border-y px-4 py-2 md:hidden">
              {navigation.map((item) => (
                <button
                  key={item.name}
                  type="button"
                  className="shrink-0 rounded-md px-3 py-2 text-xs font-medium text-muted-foreground hover:bg-accent hover:text-accent-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
                  aria-current={item.name === activeItem.name ? "page" : undefined}
                  onClick={() => setSection(item.name)}
                >
                  {item.name}
                </button>
              ))}
            </div>
            <ScrollArea className="h-0 flex-1">
              <section
                className="mx-auto w-full max-w-2xl space-y-6 p-4 pt-0 sm:p-6 sm:pt-0"
                aria-live="polite"
              >
                <div className="space-y-2 border-b pb-5 pt-5 sm:pt-6">
                  <h2 className="text-xl font-semibold tracking-tight">{activeItem.name}</h2>
                  <p className="text-sm text-muted-foreground">{activeItem.description}</p>
                </div>
                <SettingsSectionContent
                  activeItem={activeItem}
                  mounted={mounted}
                  theme={theme}
                  setTheme={setTheme}
                  onClose={() => handleOpenChange(false)}
                />
              </section>
            </ScrollArea>
          </main>
        </div>
      </DialogContent>
    </Dialog>
  );
}

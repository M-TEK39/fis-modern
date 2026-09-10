import type { ReactNode } from "react";

import { SidebarInset, SidebarProvider } from "@/components/ui/sidebar/sidebar";
import type { SessionState } from "@/lib/auth/api-auth";

type AuthenticatedSession = Extract<SessionState, { status: "authenticated" }>;

type NavigationItem = {
  label: string;
  href: string;
  roles?: readonly string[];
  accessBits?: readonly number[];
};

type NavigationGroup = {
  label: string;
  items: readonly NavigationItem[];
};

const NAVIGATION_GROUPS: readonly NavigationGroup[] = [
  {
    label: "Workspace",
    items: [
      { label: "Home", href: "/home" },
      { label: "Manuals", href: "/manuals" },
    ],
  },
  {
    label: "Fleet operations",
    items: [
      { label: "Accidents", href: "/accidents", roles: ["Accidents"], accessBits: [1] },
      { label: "Auction", href: "/auction", roles: ["Auction"], accessBits: [16] },
      { label: "Call Centre", href: "/call-centre", roles: ["Call Centre"] },
      { label: "Clearance", href: "/clearance", roles: ["Clearance"] },
      { label: "Contracts", href: "/contracts", roles: ["Contracts"], accessBits: [2] },
      {
        label: "Finance",
        href: "/finance",
        roles: [
          "Financial Reports",
          "Financial Data (Own Department)",
          "Financial Data (All Departments)",
        ],
        accessBits: [16],
      },
      { label: "Fines", href: "/fines", roles: ["Fines"], accessBits: [1] },
      { label: "Fuel Cards", href: "/fuel-cards", roles: ["Fuelcards"], accessBits: [16] },
      {
        label: "Job Cards",
        href: "/job-cards",
        roles: ["Jobcard Capturer", "Jobcard Authorizer"],
        accessBits: [1, 32],
      },
      { label: "Licenses", href: "/licenses", roles: ["Licence"] },
      { label: "Log Books", href: "/log-books", roles: ["Logbooks"], accessBits: [8] },
      { label: "Log Sheets", href: "/log-sheets", roles: ["Logsheets"], accessBits: [8] },
      { label: "Losses", href: "/losses", roles: ["Losses"] },
      { label: "Monitor", href: "/monitor", roles: ["Monitor"], accessBits: [8] },
      { label: "Private Hire", href: "/private-hire", roles: ["Private Hire Vehicles"] },
      {
        label: "Reports",
        href: "/reports",
        roles: ["Reports", "Management Reports"],
        accessBits: [8],
      },
      {
        label: "Taxis",
        href: "/taxis",
        roles: ["Private Hire Vehicles", "Taxis", "Taxi information maintenance"],
        accessBits: [1],
      },
      {
        label: "Third Party Rentals",
        href: "/third-party",
        roles: ["Third Party Rental"],
        accessBits: [2],
      },
      { label: "Towing", href: "/towing", roles: ["Towing"] },
      { label: "Tracking", href: "/tracking", accessBits: [1] },
      {
        label: "Trip Authorities",
        href: "/trip-authorities",
        roles: ["TripAuthorities", "Trip Authorities"],
        accessBits: [1],
      },
      { label: "Troubleshoot", href: "/troubleshoot", roles: ["Trouble Shooting"] },
      {
        label: "Validation Data",
        href: "/validation-data",
        roles: ["Validation"],
        accessBits: [1],
      },
      {
        label: "Vehicle Asset Verification",
        href: "/vehicle-verification",
        roles: ["Asset Verification"],
        accessBits: [1],
      },
      { label: "Vehicle Master", href: "/vehicles", roles: ["Vehicle Master"], accessBits: [1] },
      { label: "Vehicle Photos", href: "/vehicle-photos", accessBits: [1] },
      {
        label: "Full Maintenance Lease",
        href: "/full-maintenance-lease",
        roles: ["Lease Vehicle Pending", "Lease Vehicle Capturer", "Lease Vehicle Authorizer"],
        accessBits: [2],
      },
      { label: "Workshop", href: "/workshop", roles: ["Workshop"] },
    ],
  },
  {
    label: "Administration",
    items: [
      {
        label: "Driver and Authoriser Management",
        href: "/drivers",
        roles: ["Driver and Authoriser Management"],
        accessBits: [1],
      },
      { label: "User Admin", href: "/users", roles: ["User Administration"], accessBits: [4] },
      {
        label: "Notice Management",
        href: "/notice-management",
        roles: ["User Administration"],
        accessBits: [4],
      },
      { label: "Change Password", href: "/change-password" },
      { label: "Change Password and Question", href: "/change-password-question" },
    ],
  },
];

function normalize(value: string) {
  return value.trim().toLocaleLowerCase();
}

function hasRole(session: AuthenticatedSession, roles: readonly string[] | undefined) {
  if (!roles || roles.length === 0) return false;
  const availableRoles = new Set(session.roles.map(normalize));
  return roles.some((role) => availableRoles.has(normalize(role)));
}

function hasAccessBit(session: AuthenticatedSession, accessBits: readonly number[] | undefined) {
  if (!accessBits || accessBits.length === 0 || !session.accessLevel) return false;

  try {
    const accessLevel = BigInt(session.accessLevel);
    return accessBits.some((bit) => (accessLevel & BigInt(bit)) === BigInt(bit));
  } catch {
    return false;
  }
}

function canNavigate(session: AuthenticatedSession, item: NavigationItem) {
  return (
    (!item.roles && !item.accessBits) ||
    hasRole(session, item.roles) ||
    hasAccessBit(session, item.accessBits)
  );
}

function visibleGroups(session: AuthenticatedSession) {
  return NAVIGATION_GROUPS.map((group) => ({
    ...group,
    items: group.items.filter((item) => canNavigate(session, item)),
  })).filter((group) => group.items.length > 0);
}

function getInitials(value: string) {
  const parts = value
    .trim()
    .split(/\s+/)
    .map((part) => part[0])
    .filter((part): part is string => Boolean(part));

  if (parts.length > 1) return parts.slice(0, 2).join("").toUpperCase();
  return value.replace(/\s+/g, "").slice(0, 2).toUpperCase() || "FI";
}

function getSidebarUser(session: AuthenticatedSession) {
  const email = session.email?.trim() ?? "";
  const accessCode = session.userAccessCode?.trim() ?? "";
  const rawName = email ? email.split("@", 1)[0] : accessCode;
  const name = rawName.replace(/[._-]+/g, " ").trim() || "FIS user";

  return {
    name,
    email: email || accessCode || "Authenticated FIS user",
    initials: getInitials(name),
    avatar: null,
  };
}

export function getAppShellData(session: AuthenticatedSession) {
  return {
    groups: visibleGroups(session),
    user: getSidebarUser(session),
  };
}

export function AppShellFrame({
  children,
  header,
  sidebar,
}: Readonly<{ children: ReactNode; header: ReactNode; sidebar: ReactNode }>) {
  return (
    <SidebarProvider className="flex-col bg-background [--header-height:3.5rem]">
      {header}
      <div className="flex min-h-0 flex-1">
        {sidebar}
        <SidebarInset>
          <div className="fis-app-content flex min-w-0 flex-1 flex-col gap-4 p-4 md:p-6">
            {children}
          </div>
        </SidebarInset>
      </div>
    </SidebarProvider>
  );
}

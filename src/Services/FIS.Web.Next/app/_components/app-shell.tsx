import Link from "next/link";
import type { ReactNode } from "react";

import { logoutAction } from "@/app/actions/auth";
import type { SessionState } from "@/lib/api-auth";

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

function UserSummary({ session }: Readonly<{ session: AuthenticatedSession }>) {
  return (
    <div className="app-user-summary">
      <span className="app-user-label">Signed in as</span>
      <span className="app-user-name">{session.email || "FIS user"}</span>
    </div>
  );
}

export default function AppShell({
  children,
  session,
}: Readonly<{ children: ReactNode; session: AuthenticatedSession }>) {
  const groups = visibleGroups(session);

  return (
    <div className="app-shell">
      <aside className="app-sidebar">
        <div className="app-sidebar-brand">
          <Link className="app-brand-link" href="/home">
            <span className="brand-mark" aria-hidden="true">
              FIS
            </span>
            <span>
              <span className="app-brand-name">Fleet Information System</span>
              <span className="app-brand-caption">Gauteng Provincial Government</span>
            </span>
          </Link>
        </div>

        <details className="app-nav-disclosure" open>
          <summary>FIS Menu</summary>
          <nav className="app-nav" aria-label="FIS modules">
            {groups.map((group) => (
              <div className="app-nav-group" key={group.label}>
                <p className="app-nav-group-label">{group.label}</p>
                <div className="app-nav-links">
                  {group.items.map((item) => (
                    <Link className="app-nav-link" href={item.href} key={item.href}>
                      {item.label}
                    </Link>
                  ))}
                </div>
              </div>
            ))}
          </nav>
        </details>

        <div className="app-sidebar-footer">
          <UserSummary session={session} />
          <form action={logoutAction}>
            <button className="button button-secondary app-signout" type="submit">
              Sign out
            </button>
          </form>
        </div>
      </aside>

      <div className="app-content">{children}</div>
    </div>
  );
}

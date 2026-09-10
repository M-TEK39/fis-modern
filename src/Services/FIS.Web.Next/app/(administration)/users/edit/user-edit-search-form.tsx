import Link from "next/link";

import type { UserAdminProfile } from "@/lib/api/administration/api-user-admin";

type UserChoice = Pick<UserAdminProfile, "userAccessCode" | "userName" | "firstName" | "lastName">;

function userLabel(user: UserChoice) {
  const name = [user.firstName, user.lastName].filter(Boolean).join(" ");
  return user.userName ?? (name ? `${name} (${user.userAccessCode})` : String(user.userAccessCode));
}

export default function UserEditSearchForm({
  username,
  alphabet,
  users,
}: Readonly<{
  username: string;
  alphabet: string;
  users: readonly UserChoice[];
}>) {
  return (
    <form action="/users/edit" className="vehicle-status-maintenance-panel" method="get">
      <input name="Alphabet" type="hidden" value={alphabet} />
      <div className="field">
        <label htmlFor="user-edit-search">Username</label>
        <input
          id="user-edit-search"
          name="Username"
          type="search"
          list="user-edit-usernames"
          defaultValue={username}
          autoComplete="off"
          placeholder="Start typing a valid username"
          required
        />
        <datalist id="user-edit-usernames">
          {users.map((user) => (
            <option key={user.userAccessCode} value={user.userName ?? ""}>
              {userLabel(user)}
            </option>
          ))}
        </datalist>
        <p className="muted-copy">Select a username to retrieve and update its profile.</p>
      </div>
      <div className="button-row">
        <button className="button button-primary" type="submit">
          Edit
        </button>
        <Link className="button button-secondary" href="/UserAdmin/UserAdminMenu.aspx">
          Back to Menu
        </Link>
      </div>
    </form>
  );
}

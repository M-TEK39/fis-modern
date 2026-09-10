import "server-only";

import { cache } from "react";

import { validateSession } from "@/lib/auth/api-auth";

// Authentication is request-bound runtime data. Keep it out of React/Next
// cache scopes so each request reads its own HttpOnly FIS cookies.
// React.cache deduplicates the root shell and page lookup for one request only.
// It is not Next's persistent data cache and never shares authentication state
// between requests.
export const getSession = cache(validateSession);

import "server-only";

import { validateSession } from "@/lib/api-auth";

// Authentication is request-bound runtime data. Keep it out of React/Next
// cache scopes so each request reads its own HttpOnly FIS cookies.
export const getSession = validateSession;

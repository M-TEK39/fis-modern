"use client";

import { useEffect } from "react";

import { subscribeToSessionKeepAlive } from "./session-keep-alive-runtime";

// Kept as a compatibility export until session recovery can import the
// non-component refresh helper directly.
export { requestSessionRefresh } from "./session-keep-alive-runtime";

/**
 * Mount once inside the authenticated server-rendered shell. It deliberately
 * renders no UI and only keeps an existing session warm while the page is in
 * use.
 */
export default function SessionKeepAlive() {
  useEffect(() => {
    return subscribeToSessionKeepAlive();
  }, []);

  return null;
}

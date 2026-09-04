# FIS web design and accessibility rules

These rules apply to visual, interaction, and accessibility changes in the Next.js frontend. They preserve the established FIS workflow while making it clearer and more resilient.

## Preserve the workflow

- Trace the legacy module before redesigning it. Keep the same entry point, menu hierarchy, screen order, labels, and task outcomes unless the user explicitly requests a workflow change.
- Improve feedback, responsiveness, readability, and error recovery without hiding required fields or removing established actions.
- Use the same terminology as the legacy system and the C# API. Explain modern behavior in nearby help text when needed.

## Semantics and keyboard access

- Use landmarks (`header`, `nav`, `main`, `aside`, `footer`) and a logical heading hierarchy.
- Use native links, buttons, inputs, selects, tables, and dialogs before adding ARIA. Every control needs an accessible name and a visible focus indicator.
- Ensure all functionality works with keyboard alone. Keep focus order logical and return focus after closing a dialog or completing a major action.
- Do not use color alone for status, severity, required fields, errors, or success. Pair color with text, iconography, or a pattern.
- Associate labels, descriptions, validation messages, and errors with their controls. Announce asynchronous status changes when they matter to the user.
- Respect reduced-motion preferences and do not use flashing or disorienting transitions.

## Layout and responsive behavior

- Start with the narrow layout and expand it for larger screens. Avoid horizontal scrolling for ordinary forms and tables; provide an intentional responsive treatment for wide data.
- Keep primary actions visible and predictable. Do not make users hunt for Save, Cancel, Back, Search, or Retry.
- Use consistent spacing, alignment, typography, and design tokens from `src/Services/FIS.Web.Next/app/globals.css`.
- Keep touch targets comfortably usable and avoid dense controls that are difficult to activate.
- Preserve useful context while loading or submitting. Disable only the controls that would duplicate or conflict with the in-flight action.

## Forms and data-heavy pages

- Show required fields before submission and provide inline, specific, recoverable errors.
- Preserve entered values after a failed request. Never clear a complete form because the API returned an error.
- Keep table headers associated with cells, provide an empty state that explains the next action, and make row actions discoverable by keyboard and screen readers.
- Confirm destructive actions with clear consequences. Do not use confirmation dialogs for ordinary navigation.
- Use consistent date, number, currency, and identifier formatting from the existing FIS contract.

## Authentication and failure states

- Login, reset-password, expired-password, Microsoft sign-in, and session-expiry screens must explain what happened and what the user can do next without exposing sensitive details.
- API or network failure must produce an actionable error with retry/reconnect behavior where safe. Do not trap a user in a blank screen or an infinite spinner.
- Expired sessions must not silently submit stale data. Preserve safe form state where possible and guide the user back through authentication.
- Never render secrets, access tokens, or internal exception details.

## Review checklist

Before handing off a UI change, verify:

- keyboard-only operation and visible focus;
- accessible names, labels, headings, dialog semantics, and error announcements;
- loading, empty, validation, API failure, and success states;
- responsive behavior at narrow and wide widths;
- contrast and non-color status cues;
- legacy route, navigation, screen sequence, and business meaning.

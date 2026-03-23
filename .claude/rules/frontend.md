# Frontend Patterns & Anti-Patterns

## React State + Async Anti-Patterns

- **NEVER** call a function that reads state immediately after `setState` — the value hasn't updated yet. Extract the value into a local variable first.

```tsx
// Bad — stale closure
function handleSend() {
  sendMessage(input); // reads state directly
  setInput("");
}

// Good — capture value first
function handleSend() {
  const text = input.trim();
  setInput("");
  sendMessage(text);
}
```

- **Payment/confirmation callbacks** must be driven by server state (webhooks), never by click handlers. A button click should open the payment page, not trigger completion.

- **`postMessage` listeners** must validate `event.origin` against the expected server origin.

```tsx
// Bad
window.addEventListener("message", (e) => handleOAuth(e.data));

// Good
window.addEventListener("message", (e) => {
  if (e.origin !== process.env.NEXT_PUBLIC_API_URL) return;
  handleOAuth(e.data);
});
```

- **`useEffect` with fetch** should check if the component is in a terminal state before firing repeated requests.

- **Iframe `sandbox`** — never combine `allow-scripts` with `allow-same-origin` (allows the iframe to remove its own sandbox).

## Feature Isolation

- Features live in `features/{name}/` with barrel `index.ts` and colocated `__tests__/`
- Features CANNOT cross-import — enforced by ESLint `no-restricted-imports`
- `features/shared/` is the escape hatch for cross-feature utilities
- Agent-site exception: `templates/` can import from `sections/` subsection barrels (NOT top-level `sections/index.ts`)

## Import Patterns

- Barrel exports: named re-exports only (`export { X } from './X'`), never `export *`
- Templates: import from subsection barrels (`@/features/sections/heroes`), never top-level barrel
- Dynamic imports for templates (`next/dynamic`), Turnstile (`ssr: false`), Sentry (lazy in catch)
- `app/` route files: thin composition — import from features, no business logic

## Styling

- Agent-site section components use inline `style={}` with CSS custom properties for runtime branding
- This is the correct pattern for white-label multi-tenant sites — NOT a migration target
- Config-driven branding values (`var(--color-primary)`) are runtime values, not static styles

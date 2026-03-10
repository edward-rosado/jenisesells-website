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

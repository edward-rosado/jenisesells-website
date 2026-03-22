export type TurnstileResult = { ok: true } | { ok: false; code: string; detail?: string };

export async function validateTurnstile(token: string): Promise<TurnstileResult> {
  const secret = process.env.TURNSTILE_SECRET_KEY;
  if (!secret) {
    return { ok: false, code: "SEC-002", detail: "TURNSTILE_SECRET_KEY is not set" };
  }

  if (!token) {
    return { ok: false, code: "SEC-003", detail: "Turnstile token is empty" };
  }

  const controller = new AbortController();
  const timeoutId = setTimeout(() => controller.abort(), 15_000);
  try {
    const res = await fetch("https://challenges.cloudflare.com/turnstile/v0/siteverify", {
      method: "POST",
      headers: { "Content-Type": "application/x-www-form-urlencoded" },
      body: new URLSearchParams({ secret, response: token }),
      signal: controller.signal,
    });
    const data = await res.json();
    if (!data.success) {
      return {
        ok: false,
        code: "SEC-004",
        detail: JSON.stringify({
          errorCodes: data["error-codes"],
          hostname: data.hostname,
        }),
      };
    }
    return { ok: true };
  } catch (error) {
    return { ok: false, code: "SEC-001", detail: String(error) };
  } finally {
    clearTimeout(timeoutId);
  }
}

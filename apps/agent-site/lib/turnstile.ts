export async function validateTurnstile(token: string): Promise<boolean> {
  const secret = process.env.TURNSTILE_SECRET_KEY;
  if (!secret) return false;

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
    return data.success === true;
  } catch (error) {
    console.error("[SEC-001] Turnstile validation error:", error);
    return false;
  } finally {
    clearTimeout(timeoutId);
  }
}

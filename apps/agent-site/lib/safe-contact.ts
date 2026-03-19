const EMAIL_RE = /^[^\s@:]+@[^\s@:]+\.[^\s@:]+$/;

export function safeMailtoHref(email: string): string {
  if (!email || !EMAIL_RE.test(email)) return "#";
  return `mailto:${email}`;
}

export function safeTelHref(phone: string, ext?: string): string {
  const digits = phone.replace(/\D/g, "");
  if (!digits) return "#";
  const extSuffix = ext ? `,${ext.replace(/\D/g, "")}` : "";
  return `tel:${digits}${extSuffix}`;
}

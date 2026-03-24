interface MarkdownContentProps {
  content: string;
}

/**
 * Lightweight server-generated markdown renderer.
 * Only handles the subset used in legal pages: headings, bold, links, lists, paragraphs.
 * Content is entirely server-generated (not user input) — safe for innerHTML.
 * Replaces react-markdown to reduce Cloudflare Worker bundle size.
 */
export function MarkdownContent({ content }: MarkdownContentProps) {
  if (!content?.trim()) return null;

  // SECURITY: content is server-generated template literals from legal pages only.
  // Never pass user-supplied content to this component.
  const html = markdownToHtml(content);

  return (
    <div
      className="prose max-w-none prose-headings:text-gray-900 prose-h2:text-xl prose-h2:font-semibold prose-h2:border-b prose-h2:border-gray-200 prose-h2:pb-2 prose-h3:text-lg prose-h3:font-medium prose-p:text-base prose-p:text-gray-600 prose-p:leading-relaxed prose-a:text-emerald-700 prose-strong:text-gray-900 prose-li:text-gray-600 prose-ul:ml-6 prose-ul:space-y-2"
      dangerouslySetInnerHTML={{ __html: html }}
    />
  );
}

function markdownToHtml(md: string): string {
  const lines = md.split("\n");
  const out: string[] = [];
  let inList = false;

  for (let i = 0; i < lines.length; i++) {
    const line = lines[i];

    // Headings
    const headingMatch = line.match(/^(#{1,6})\s+(.+)$/);
    if (headingMatch) {
      if (inList) { out.push("</ul>"); inList = false; }
      const level = headingMatch[1].length;
      out.push(`<h${level}>${inlineFormat(headingMatch[2])}</h${level}>`);
      continue;
    }

    // List items
    if (line.match(/^[-*]\s+/)) {
      if (!inList) { out.push("<ul>"); inList = true; }
      out.push(`<li>${inlineFormat(line.replace(/^[-*]\s+/, ""))}</li>`);
      continue;
    }

    // Close list if we hit a non-list line
    if (inList) { out.push("</ul>"); inList = false; }

    // Blank line
    if (line.trim() === "") continue;

    // Paragraph
    out.push(`<p>${inlineFormat(line)}</p>`);
  }

  if (inList) out.push("</ul>");
  return out.join("");
}

function inlineFormat(text: string): string {
  // Bold
  let result = text.replace(/\*\*(.+?)\*\*/g, "<strong>$1</strong>");
  // Italic
  result = result.replace(/\*(.+?)\*/g, "<em>$1</em>");
  // Links
  result = result.replace(/\[([^\]]+)\]\(([^)]+)\)/g, (_, text, href) => {
    const safeHref = /^https?:\/\//i.test(href) || href.startsWith('/') || href.startsWith('#') ? href : '#';
    return `<a href="${safeHref}">${text}</a>`;
  });
  return result;
}

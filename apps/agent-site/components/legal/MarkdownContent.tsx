import ReactMarkdown from "react-markdown";

interface MarkdownContentProps {
  content: string;
}

export function MarkdownContent({ content }: MarkdownContentProps) {
  if (!content?.trim()) return null;

  return (
    <div className="prose max-w-none prose-headings:text-gray-900 prose-h2:text-xl prose-h2:font-semibold prose-h2:border-b prose-h2:border-gray-200 prose-h2:pb-2 prose-h3:text-lg prose-h3:font-medium prose-p:text-base prose-p:text-gray-600 prose-p:leading-relaxed prose-a:text-emerald-700 prose-strong:text-gray-900 prose-li:text-gray-600 prose-ul:ml-6 prose-ul:space-y-2">
      <ReactMarkdown>{content}</ReactMarkdown>
    </div>
  );
}

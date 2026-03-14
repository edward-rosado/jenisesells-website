import ReactMarkdown from "react-markdown";

interface MarkdownContentProps {
  content: string;
}

export function MarkdownContent({ content }: MarkdownContentProps) {
  if (!content?.trim()) return null;

  return (
    <div className="prose prose-invert max-w-none prose-headings:text-white prose-p:text-gray-300 prose-a:text-emerald-400 prose-strong:text-white prose-li:text-gray-300">
      <ReactMarkdown>{content}</ReactMarkdown>
    </div>
  );
}

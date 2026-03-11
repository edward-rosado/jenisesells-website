"use client";

import ReactMarkdown from "react-markdown";

export default function TermsContent({ markdown }: { markdown: string }) {
  return (
    <ReactMarkdown
      components={{
        h1: ({ children }) => (
          <h1 className="text-3xl font-bold text-white mb-2">{children}</h1>
        ),
        h2: ({ children }) => (
          <h2 className="text-xl font-semibold text-white mt-10 mb-3 border-b border-gray-700 pb-2">
            {children}
          </h2>
        ),
        h3: ({ children }) => (
          <h3 className="text-base font-semibold text-gray-200 mt-6 mb-2">
            {children}
          </h3>
        ),
        p: ({ children }) => (
          <p className="text-gray-300 leading-relaxed mb-4">{children}</p>
        ),
        ul: ({ children }) => (
          <ul className="list-disc list-inside text-gray-300 mb-4 space-y-1">
            {children}
          </ul>
        ),
        ol: ({ children }) => (
          <ol className="list-decimal list-inside text-gray-300 mb-4 space-y-1">
            {children}
          </ol>
        ),
        li: ({ children }) => <li className="leading-relaxed">{children}</li>,
        strong: ({ children }) => (
          <strong className="text-white font-semibold">{children}</strong>
        ),
        blockquote: ({ children }) => (
          <blockquote className="border-l-4 border-yellow-500 pl-4 my-4 text-yellow-300 italic">
            {children}
          </blockquote>
        ),
        hr: () => <hr className="border-gray-700 my-8" />,
        a: ({ href, children }) => (
          <a
            href={href}
            className="text-emerald-400 hover:text-emerald-300 underline"
            target={href?.startsWith("http") ? "_blank" : undefined}
            rel={href?.startsWith("http") ? "noopener noreferrer" : undefined}
          >
            {children}
          </a>
        ),
        table: ({ children }) => (
          <div className="overflow-x-auto mb-4">
            <table className="w-full text-sm text-gray-300 border border-gray-700">
              {children}
            </table>
          </div>
        ),
        th: ({ children }) => (
          <th className="px-4 py-2 bg-gray-800 text-white font-semibold border border-gray-700 text-left">
            {children}
          </th>
        ),
        td: ({ children }) => (
          <td className="px-4 py-2 border border-gray-700">{children}</td>
        ),
      }}
    >
      {markdown}
    </ReactMarkdown>
  );
}

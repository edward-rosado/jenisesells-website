"use client";

import { useState, useRef, useEffect } from "react";
import { MessageRenderer, type ChatMessageData } from "./MessageRenderer";

interface ChatWindowProps {
  sessionId: string;
  initialMessages: ChatMessageData[];
}

const API_BASE = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5000";

export function ChatWindow({ sessionId, initialMessages }: ChatWindowProps) {
  const [messages, setMessages] = useState<ChatMessageData[]>(initialMessages);
  const [input, setInput] = useState("");
  const [sending, setSending] = useState(false);
  const scrollRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    scrollRef.current?.scrollTo?.(0, scrollRef.current.scrollHeight);
  }, [messages]);

  async function handleSend() {
    const text = input.trim();
    if (!text || sending) return;

    const userMsg: ChatMessageData = { role: "user", content: text };
    setMessages((prev) => [...prev, userMsg]);
    setInput("");
    setSending(true);

    try {
      const res = await fetch(`${API_BASE}/onboard/${sessionId}/chat`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ message: text }),
      });

      if (!res.ok) {
        setMessages((prev) => [
          ...prev,
          { role: "assistant", content: "Something went wrong. Please try again." },
        ]);
        return;
      }

      const contentType = res.headers.get("content-type") ?? "";

      if (contentType.includes("text/event-stream")) {
        // SSE streaming response
        const reader = res.body?.getReader();
        if (!reader) return;

        const decoder = new TextDecoder();
        let buffer = "";
        let assistantContent = "";

        // Add placeholder assistant message
        setMessages((prev) => [...prev, { role: "assistant", content: "" }]);

        while (true) {
          const { done, value } = await reader.read();
          if (done) break;

          buffer += decoder.decode(value, { stream: true });
          const lines = buffer.split("\n");
          buffer = lines.pop() ?? "";

          for (const line of lines) {
            if (!line.startsWith("data: ")) continue;
            const data = line.slice(6);
            if (data === "[DONE]") continue;

            assistantContent += data;
            const updatedContent = assistantContent;
            setMessages((prev) => {
              const next = [...prev];
              next[next.length - 1] = { role: "assistant", content: updatedContent };
              return next;
            });
          }
        }
      } else {
        // JSON response (fallback)
        const data = await res.json();
        setMessages((prev) => [
          ...prev,
          { role: "assistant", content: data.response },
        ]);
      }
    } finally {
      setSending(false);
    }
  }

  function handleAction(action: string, data?: unknown) {
    // Send the action as a chat message so Claude can process it
    const text = data ? `[Action: ${action}] ${JSON.stringify(data)}` : `[Action: ${action}]`;
    setInput(text);
    handleSend();
  }

  return (
    <main className="flex flex-col h-screen bg-gray-950 text-white">
      <div ref={scrollRef} className="flex-1 overflow-y-auto p-4 space-y-3">
        {messages.map((msg, i) => (
          <MessageRenderer key={i} message={msg} onAction={handleAction} />
        ))}
        {sending && (
          <div className="flex justify-start">
            <div className="bg-gray-800 rounded-2xl px-4 py-2 text-gray-400">
              <span className="animate-pulse">Thinking...</span>
            </div>
          </div>
        )}
      </div>
      <form
        onSubmit={(e) => {
          e.preventDefault();
          handleSend();
        }}
        className="p-4 border-t border-gray-800 flex gap-2"
      >
        <input
          type="text"
          value={input}
          onChange={(e) => setInput(e.target.value)}
          placeholder="Type a message..."
          className="flex-1 px-4 py-2 rounded-lg bg-gray-800 border border-gray-700 text-white placeholder-gray-500 focus:outline-none focus:ring-2 focus:ring-emerald-500"
        />
        <button
          type="submit"
          disabled={sending}
          className="px-4 py-2 rounded-lg bg-emerald-600 hover:bg-emerald-500 text-white font-semibold disabled:opacity-50 transition-colors"
        >
          Send
        </button>
      </form>
    </main>
  );
}

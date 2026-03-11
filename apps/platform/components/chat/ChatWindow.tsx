"use client";

import { useState, useRef, useEffect } from "react";
import { MessageRenderer, type ChatMessageData } from "./MessageRenderer";

interface ChatWindowProps {
  sessionId: string;
  token: string;
  initialMessages: ChatMessageData[];
}

const API_BASE = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5000";

// Matches [CARD:type] followed by a JSON object
const CARD_MARKER = /\[CARD:(\w+)\]/g;

/** Extract a JSON object starting at position `start` in `str` */
function extractJson(str: string, start: number): { json: string; end: number } | null {
  if (str[start] !== "{") return null;
  let depth = 0;
  let inString = false;
  let escape = false;
  for (let i = start; i < str.length; i++) {
    const ch = str[i];
    if (escape) { escape = false; continue; }
    if (ch === "\\") { escape = true; continue; }
    if (ch === '"') { inString = !inString; continue; }
    if (inString) continue;
    if (ch === "{") depth++;
    if (ch === "}") { depth--; if (depth === 0) return { json: str.slice(start, i + 1), end: i + 1 }; }
  }
  return null;
}

/** Strip [Tool: ...] lines from visible text */
function cleanToolLines(text: string): string {
  return text.replace(/\n?\[Tool: [^\]]+\][^\n]*/g, "").trim();
}

/** Parse a completed assistant response into text + card segments */
function parseAssistantContent(content: string): ChatMessageData[] {
  const messages: ChatMessageData[] = [];
  let lastIndex = 0;

  for (const match of content.matchAll(CARD_MARKER)) {
    const markerStart = match.index ?? 0;
    const markerEnd = markerStart + match[0].length;
    const cardType = match[1] as ChatMessageData["type"];

    // Text before the card marker
    const textBefore = cleanToolLines(content.slice(lastIndex, markerStart));
    if (textBefore) {
      messages.push({ role: "assistant", content: textBefore });
    }

    // Try to parse JSON immediately after the marker
    const extracted = extractJson(content, markerEnd);
    if (extracted) {
      try {
        const metadata = JSON.parse(extracted.json) as Record<string, unknown>;
        messages.push({ role: "assistant", content: "", type: cardType, metadata });
      } catch {
        messages.push({ role: "assistant", content: match[0] });
      }
      lastIndex = extracted.end;
    } else {
      lastIndex = markerEnd;
    }
  }

  // Remaining text after last card
  const remaining = cleanToolLines(content.slice(lastIndex));
  if (remaining) {
    messages.push({ role: "assistant", content: remaining });
  }

  // No cards found — return as single text message
  if (messages.length === 0 && content.trim()) {
    const cleaned = cleanToolLines(content);
    if (cleaned) {
      messages.push({ role: "assistant", content: cleaned });
    }
  }

  return messages;
}

export function ChatWindow({ sessionId, token, initialMessages }: ChatWindowProps) {
  const [messages, setMessages] = useState<ChatMessageData[]>(initialMessages);
  const [input, setInput] = useState("");
  const [sending, setSending] = useState(false);
  const scrollRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    scrollRef.current?.scrollTo?.(0, scrollRef.current.scrollHeight);
  }, [messages]);

  async function sendMessage(text: string) {
    if (!text || sending) return;

    const userMsg: ChatMessageData = { role: "user", content: text };
    setMessages((prev) => [...prev, userMsg]);
    setInput("");
    setSending(true);

    try {
      const res = await fetch(`${API_BASE}/onboard/${sessionId}/chat`, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          "Authorization": `Bearer ${token}`,
        },
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
        const reader = res.body?.getReader();
        if (!reader) return;

        const decoder = new TextDecoder();
        let buffer = "";
        let assistantContent = "";

        // Show streaming placeholder
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

            let decoded: string;
            try {
              decoded = JSON.parse(data) as string;
            } catch {
              decoded = data;
            }
            assistantContent += decoded;

            // During streaming, show raw text (cards render after stream completes)
            const updatedContent = assistantContent;
            setMessages((prev) => {
              const next = [...prev];
              next[next.length - 1] = { role: "assistant", content: updatedContent };
              return next;
            });
          }
        }

        // Stream complete — parse content into text + card segments
        const parsed = parseAssistantContent(assistantContent);
        if (parsed.length > 0) {
          setMessages((prev) => {
            // Remove the streaming placeholder and append parsed segments
            const withoutPlaceholder = prev.slice(0, -1);
            return [...withoutPlaceholder, ...parsed];
          });
        }
      } else {
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

  function handleSend() {
    sendMessage(input.trim());
  }

  function handleAction(action: string, data?: unknown) {
    const text = data ? `[Action: ${action}] ${JSON.stringify(data)}` : `[Action: ${action}]`;
    sendMessage(text);
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

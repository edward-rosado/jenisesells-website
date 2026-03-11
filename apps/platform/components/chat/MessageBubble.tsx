import { GeometricStar } from "@/components/GeometricStar";

interface MessageBubbleProps {
  role: "user" | "assistant";
  content: string;
}

export function MessageBubble({ role, content }: MessageBubbleProps) {
  const isUser = role === "user";
  return (
    <div className={`flex ${isUser ? "justify-end" : "justify-start"} gap-2`}>
      {!isUser && (
        <div className="flex-shrink-0 mt-1">
          <GeometricStar className="w-6 h-6" />
        </div>
      )}
      <div
        className={`max-w-[80%] rounded-2xl px-4 py-2 ${
          isUser
            ? "bg-emerald-600 text-white"
            : "bg-gray-800 text-gray-100"
        }`}
      >
        <span className="whitespace-pre-wrap">{content}</span>
      </div>
    </div>
  );
}

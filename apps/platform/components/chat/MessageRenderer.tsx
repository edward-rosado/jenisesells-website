import { MessageBubble } from "./MessageBubble";
import { ProfileCard } from "./ProfileCard";
import { ColorPalette } from "./ColorPalette";
import { SitePreview } from "./SitePreview";
import { FeatureChecklist } from "./FeatureChecklist";
import { PaymentCard } from "./PaymentCard";

export interface ChatMessageData {
  role: "user" | "assistant";
  content: string;
  type?: "text" | "profile_card" | "color_palette" | "site_preview" | "feature_checklist" | "payment_card";
  metadata?: Record<string, unknown>;
}

interface MessageRendererProps {
  message: ChatMessageData;
  onAction?: (action: string, data?: unknown) => void;
}

export function MessageRenderer({ message, onAction }: MessageRendererProps) {
  const meta = message.metadata ?? {};
  const act = onAction ?? (() => {});

  switch (message.type) {
    case "profile_card":
      return (
        <ProfileCard
          name={(meta.name as string) ?? ""}
          brokerage={meta.brokerage as string}
          state={meta.state as string}
          photoUrl={meta.photoUrl as string}
          homesSold={meta.homesSold as number}
          avgRating={meta.avgRating as number}
          onConfirm={() => act("confirm_profile")}
        />
      );
    case "color_palette":
      return (
        <ColorPalette
          primaryColor={(meta.primaryColor as string) ?? "#000000"}
          accentColor={(meta.accentColor as string) ?? "#000000"}
          onConfirm={(colors) => act("confirm_colors", colors)}
        />
      );
    case "site_preview":
      return (
        <SitePreview
          siteUrl={(meta.siteUrl as string) ?? ""}
          onApprove={() => act("approve_site")}
        />
      );
    case "feature_checklist":
      return <FeatureChecklist />;
    case "payment_card":
      return <PaymentCard onPaymentComplete={() => act("payment_complete")} />;
    default:
      return <MessageBubble role={message.role} content={message.content} />;
  }
}

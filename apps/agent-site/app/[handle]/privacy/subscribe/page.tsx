import type { Metadata } from "next";
import { SubscribeForm } from "@/features/privacy/SubscribeForm";
import { loadAccountConfig } from "@/features/config/config";

interface PageProps {
  params: Promise<{ handle: string }>;
  searchParams: Promise<{ email?: string; token?: string }>;
}

export async function generateMetadata({ params }: PageProps): Promise<Metadata> {
  const { handle } = await params;
  try {
    const account = loadAccountConfig(handle);
    const name = account.agent?.name ?? account.broker?.name ?? account.brokerage.name;
    return { title: `Re-subscribe | ${name}` };
  } catch {
    return { title: "Re-subscribe" };
  }
}

export default async function SubscribePage({ params, searchParams }: PageProps) {
  const { handle } = await params;
  const { email, token } = await searchParams;

  let agentName = "your agent";
  try {
    const account = loadAccountConfig(handle);
    agentName = account.agent?.name ?? account.broker?.name ?? account.brokerage.name;
  } catch {
    // fall through with default name
  }

  return (
    <main className="min-h-screen flex items-center justify-center px-4 py-16">
      <div className="max-w-md w-full">
        <h1 className="text-2xl font-bold mb-4">Re-subscribe to Communications</h1>
        <p className="text-gray-600 mb-6">
          You are about to re-subscribe to communications from{" "}
          <strong>{agentName}</strong>. You will receive updates and market
          information relevant to your real estate needs.
        </p>
        <SubscribeForm agentId={handle} email={email ?? ""} token={token ?? ""} />
      </div>
    </main>
  );
}

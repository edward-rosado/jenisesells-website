import { StatusDashboard } from "./StatusDashboard";

export const metadata = {
  title: "System Status — Real Estate Star",
  description: "Live status of Real Estate Star services",
};

export default function StatusPage() {
  return (
    <main className="min-h-screen bg-gray-950 text-white px-4 py-16">
      <div className="max-w-2xl mx-auto">
        <h1 className="text-4xl font-bold mb-2">System Status</h1>
        <p className="text-gray-400 mb-8">
          Real-time health of Real Estate Star services
        </p>
        <StatusDashboard />
      </div>
    </main>
  );
}

export function LegalPageLayout({ children }: { children: React.ReactNode }) {
  return (
    <main className="min-h-screen pt-24 pb-16 px-4">
      <div className="mx-auto max-w-3xl prose-invert">
        {children}
      </div>
    </main>
  );
}

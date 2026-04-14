export default function AuthLayout({ children }: { children: React.ReactNode }) {
  return (
    <div
      className="min-h-screen flex items-center justify-center px-4 py-12"
      style={{ background: 'var(--bg)' }}
    >
      {/* Decorative background */}
      <div
        className="fixed inset-0 pointer-events-none"
        style={{
          background:
            'radial-gradient(ellipse 60% 50% at 50% -10%, rgba(255,92,26,0.08) 0%, transparent 70%)',
        }}
      />
      <div className="relative w-full max-w-md">{children}</div>
    </div>
  )
}

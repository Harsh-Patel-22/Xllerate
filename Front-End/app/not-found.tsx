import Link from 'next/link'
import { Activity } from 'lucide-react'

export default function NotFound() {
  return (
    <div
      className="min-h-screen flex flex-col items-center justify-center px-4 text-center"
      style={{ background: 'var(--bg)' }}
    >
      {/* Glow */}
      <div
        className="fixed inset-0 pointer-events-none"
        style={{
          background:
            'radial-gradient(ellipse 50% 40% at 50% 0%, rgba(255,92,26,0.07) 0%, transparent 70%)',
        }}
      />

      <div className="relative">
        {/* Logo mark */}
        <div className="flex justify-center mb-8">
          <div
            className="w-12 h-12 rounded-2xl flex items-center justify-center"
            style={{ background: 'var(--orange-dim)', border: '1px solid var(--orange-glow)' }}
          >
            <Activity size={22} style={{ color: 'var(--orange)' }} />
          </div>
        </div>

        {/* 404 */}
        <p
          className="font-syne font-extrabold mb-3"
          style={{ fontSize: '96px', color: 'var(--bg-4)', lineHeight: 1 }}
        >
          404
        </p>

        <h1
          className="font-syne font-bold mb-2"
          style={{ fontSize: '24px', color: 'var(--white)' }}
        >
          Page not found
        </h1>
        <p
          className="mb-8 max-w-xs mx-auto"
          style={{ fontSize: '14px', color: 'var(--white-3)', lineHeight: 1.7 }}
        >
          This page doesn&apos;t exist or has been moved. Let&apos;s get you back on track.
        </p>

        <Link
          href="/dashboard/home"
          className="inline-flex items-center gap-2 px-6 py-2.5 rounded-xl font-syne font-semibold text-white transition-opacity hover:opacity-90"
          style={{ background: 'var(--orange)', fontSize: '14px' }}
        >
          Back to Dashboard
        </Link>
      </div>
    </div>
  )
}

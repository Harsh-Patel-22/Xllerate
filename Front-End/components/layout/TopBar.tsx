'use client'
import { useUIStore } from '@/store/uiStore'
import { Menu, Bell, Search } from 'lucide-react'

interface TopBarProps {
  title: string
  subtitle?: string
  action?: React.ReactNode
}

export default function TopBar({ title, subtitle, action }: TopBarProps) {
  const { toggleSidebar } = useUIStore()

  return (
    <header
      className="sticky top-0 z-30 flex items-center justify-between px-4 sm:px-6 lg:px-8 py-4"
      style={{
        background: 'rgba(10,10,10,0.85)',
        backdropFilter: 'blur(12px)',
        borderBottom: '1px solid var(--border)',
      }}
    >
      <div className="flex items-center gap-3 min-w-0">
        {/* Hamburger – mobile only */}
        <button
          onClick={toggleSidebar}
          className="lg:hidden p-2 rounded-lg flex-shrink-0 transition-colors"
          style={{ color: 'var(--white-2)' }}
          aria-label="Open menu"
        >
          <Menu size={20} />
        </button>

        <div className="min-w-0">
          <h1
            className="font-syne font-bold truncate"
            style={{ fontSize: '18px', color: 'var(--white)', lineHeight: 1.2 }}
          >
            {title}
          </h1>
          {subtitle && (
            <p
              className="truncate mt-0.5"
              style={{ fontSize: '12px', color: 'var(--white-3)' }}
            >
              {subtitle}
            </p>
          )}
        </div>
      </div>

      <div className="flex items-center gap-2 flex-shrink-0">
        <button
          className="hidden sm:flex p-2 rounded-lg transition-colors"
          style={{ color: 'var(--white-3)' }}
          aria-label="Search"
        >
          <Search size={17} />
        </button>
        {action && <div className="ml-1">{action}</div>}
      </div>
    </header>
  )
}

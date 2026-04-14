'use client'
import Link from 'next/link'
import { usePathname } from 'next/navigation'
import { useUIStore } from '@/store/uiStore'
import { cn } from '@/lib/utils'
import {
  LayoutGrid,
  FileText,
  BarChart2,
  Map,
  Briefcase,
  User,
  Activity,
  X,
  LogOut,
} from 'lucide-react'
import { useRouter } from "next/navigation";
import { Button } from '../ui'
import { useState, useEffect } from 'react'

const NAV_ITEMS = [
  { label: 'Overview', href: '/dashboard/home', icon: LayoutGrid, section: 'Core' },
  { label: 'Resume', href: '/dashboard/resume', icon: FileText, section: 'Core', dot: 'green' },
  { label: 'Analysis', href: '/dashboard/analysis', icon: BarChart2, section: 'Core' },
  { label: 'Roadmap', href: '/dashboard/roadmap', icon: Map, section: 'Core' },
  { label: 'Jobs', href: '/dashboard/jobs', icon: Briefcase, section: 'Explore' },
]

function SidebarContent({ onClose }: { onClose?: () => void }) {
  const pathname = usePathname()
  const { user } = useUIStore()
  const router = useRouter();
  const [userName, setUserName] = useState(user.name)
  const sections = ['Core', 'Explore']

  useEffect(() => {
    const token =
      typeof window !== "undefined" ? localStorage.getItem("token") : null;

    if (!token) return;

    try {
      const payload = token.split(".")[1];

      // Decode base64 safely
      const decodedPayload = JSON.parse(atob(payload));

      console.log("Decoded JWT:", decodedPayload);

      // Try all common name fields
      const name =
        decodedPayload.name ||
        decodedPayload.fullName ||
        decodedPayload.FullName ||
        decodedPayload.username ||
        decodedPayload["http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name"];

      if (name) {
        setUserName(name);
      }
    } catch (error) {
      console.error("JWT decode failed:", error);
    }
  }, []);

  return (
    <div className="flex flex-col h-full flex-1 w-full">
      {/* Logo */}
      <div
        className="flex items-center justify-between px-5 py-6"
        style={{ borderBottom: '1px solid var(--border)' }}
      >
        <div className="flex items-center gap-3">
          <div
            className="w-8 h-8 rounded-lg flex items-center justify-center flex-shrink-0"
            style={{ background: 'var(--orange)' }}
          >
            <Activity size={16} color="#fff" strokeWidth={2.5} />
          </div>
          <Button
            className="text-lg font-syne font-extrabold"
            style={{ color: 'var(--white)', marginLeft: '0px', paddingLeft: '0px' }}
            onClick={() => router.push('/dashboard/home')}
          >
            <span>Xll<span style={{ color: 'var(--orange)' }}>er</span>ate</span>
          </Button>
        </div>
        {onClose && (
          <button
            onClick={onClose}
            className="p-1 rounded-lg lg:hidden"
            style={{ color: 'var(--white-3)' }}
          >
            <X size={18} />
          </button>
        )}
      </div>

      {/* Nav */}
      <nav className="flex-1 px-3 py-4 overflow-y-auto">
        {sections.map((section) => (
          <div key={section} className="mb-2">
            <div
              className="px-2 py-1.5 mb-1 font-syne font-semibold"
              style={{
                fontSize: '10px',
                letterSpacing: '0.1em',
                textTransform: 'uppercase',
                color: 'var(--white-3)',
              }}
            >
              {section}
            </div>
            {NAV_ITEMS.filter((n) => n.section === section).map((item) => {
              const Icon = item.icon
              const active = pathname === item.href
              return (
                <Link
                  key={item.href}
                  href={item.href}
                  onClick={onClose}
                  className={cn(
                    'flex items-center gap-2.5 px-2.5 py-2 rounded-lg mb-0.5 transition-all duration-150',
                    active
                      ? 'text-orange'
                      : 'hover:text-white'
                  )}
                  style={{
                    fontSize: '13px',
                    fontWeight: active ? 500 : 400,
                    color: active ? 'var(--orange)' : 'var(--white-2)',
                    background: active ? 'var(--orange-dim)' : 'transparent',
                  }}
                >
                  <Icon
                    size={15}
                    style={{ opacity: active ? 1 : 0.65, flexShrink: 0 }}
                  />
                  <span className="flex-1">{item.label}</span>
                  {item.dot && (
                    <span
                      className="w-1.5 h-1.5 rounded-full flex-shrink-0"
                      style={{ background: 'var(--green)' }}
                    />
                  )}
                </Link>
              )
            })}
          </div>
        ))}
      </nav>

      {/* User */}
      <div
        className="p-4"
        style={{ borderTop: '1px solid var(--border)' }}
      >
        <div className="flex items-center gap-2.5">
          <Button
            className="w-8 h-8 rounded-full flex items-center justify-center flex-shrink-0 font-syne font-bold text-xs text-white"
            style={{
              background: 'linear-gradient(135deg, var(--orange), #FF9560)',
            }}
            onClick={() => { router.push("/dashboard/profile") }}
          >
            {userName
              .split(' ')
              .map((n: string) => n[0])
              .join('')
              .substring(0, 2)}
          </Button>
          <div className="flex-1 min-w-0">
            <Button
              className="font-medium truncate"
              onClick={() => { router.push("/dashboard/profile") }}
              style={{ fontSize: '13px', color: 'var(--white)', marginLeft: '0px', paddingLeft: '0px', marginBottom: '-2.5px' }}
            >
              {userName}
            </Button>
            <p
              className="font-medium truncate"
              style={{ fontSize: '11.5px', color: 'var(--white)', marginTop: '0px' }}>
              Edit your profile!
            </p>
            <p
              className="font-semibold capitalize"
              style={{ fontSize: '11px', color: 'var(--orange)', marginTop: '2px' }}
            >
              {user.plan} Plan
            </p>
          </div>

          <button
            title="Log out"
            onClick={() => {
              localStorage.removeItem('token')
              router.push('/auth/login')
            }}
            className="p-2 rounded-lg opacity-60 hover:opacity-100 hover:bg-white/5 transition-all text-red-500"
          >
            <LogOut size={16} color="var(--red)" />
          </button>
        </div>
      </div>
    </div>
  )
}

export default function Sidebar() {
  const { sidebarOpen, setSidebarOpen } = useUIStore()

  return (
    <>
      {/* Spacer for fixed desktop sidebar */}
      <div className="hidden lg:block w-56 flex-shrink-0" />

      {/* Desktop sidebar */}
      <aside
        className="hidden lg:flex flex-col w-56 flex-shrink-0 fixed top-0 left-0 h-screen z-40"
        style={{
          background: 'var(--bg-2)',
          borderRight: '1px solid var(--border)',
        }}
      >
        <SidebarContent />
      </aside>

      {/* Mobile overlay */}
      {sidebarOpen && (
        <div
          className="sidebar-overlay lg:hidden"
          onClick={() => setSidebarOpen(false)}
        />
      )}

      {/* Mobile drawer */}
      <aside
        className={cn(
          'fixed top-0 left-0 h-full w-64 z-50 flex flex-col lg:hidden transition-transform duration-300',
          sidebarOpen ? 'translate-x-0' : '-translate-x-full'
        )}
        style={{
          background: 'var(--bg-2)',
          borderRight: '1px solid var(--border)',
        }}
      >
        <SidebarContent onClose={() => setSidebarOpen(false)} />
      </aside>
    </>
  )
}

'use client'
import { useRouter } from 'next/navigation'
import { useUIStore } from '@/store/uiStore'

/**
 * useAuth — provides login/logout helpers.
 * Wire up to authApi.login() / authApi.register() for real auth.
 */
export function useAuth() {
  const router = useRouter()
  const { user } = useUIStore()

  const logout = () => {
    if (typeof window !== 'undefined') {
      localStorage.removeItem('xllerate_token')
    }
    router.push('/auth/login')
  }

  const isAuthenticated = !!user

  return { user, isAuthenticated, logout }
}

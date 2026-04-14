'use client'

import { useEffect, useState } from 'react'
import { useRouter, usePathname } from 'next/navigation'

const PUBLIC_ROUTES = ['/auth/login', '/auth/register']

export default function ProtectedRoute({ children }: { children: React.ReactNode }) {
  const router = useRouter()
  const pathname = usePathname()
  const [isAuthorized, setIsAuthorized] = useState(false)

  useEffect(() => {
    const token = localStorage.getItem('token')

    if (PUBLIC_ROUTES.includes(pathname)) {
      if (token) {
        // Automatically redirect logged-in users away from auth pages
        router.push('/dashboard/home')  
      } else {
        setIsAuthorized(true)
      }
      return
    }

    if (!token) {
      router.push('/auth/login')
    } else {
      setIsAuthorized(true)
    }
  }, [pathname, router])

  if (!isAuthorized) {
    return null // Return a loading spinner here in the future if desired
  }

  return <>{children}</>
}

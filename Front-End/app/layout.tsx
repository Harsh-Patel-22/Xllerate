import type { Metadata } from 'next'
import { Toaster } from 'sonner'
import './globals.css'
import ProtectedRoute from '@/components/auth/ProtectedRoute'

export const metadata: Metadata = {
  title: 'Xllerate — Career Diagnosis Engine',
  description: "Know exactly why you won't get hired — and how to fix it.",
}

export default function RootLayout({
  children,
}: {
  children: React.ReactNode
}) {
  return (
    <html lang="en">
      <head>
        <link rel="preconnect" href="https://fonts.googleapis.com" />
        <link rel="preconnect" href="https://fonts.gstatic.com" crossOrigin="anonymous" />
        <link
          href="https://fonts.googleapis.com/css2?family=Syne:wght@400;500;600;700;800&family=DM+Sans:ital,opsz,wght@0,9..40,300;0,9..40,400;0,9..40,500;1,9..40,300&display=swap"
          rel="stylesheet"
        />
      </head>
      <body>
        <Toaster theme="dark" position="top-center" richColors />
        <ProtectedRoute>
          {children}
        </ProtectedRoute>
      </body>
    </html>
  )
}

'use client'
import { useState } from 'react'
import Link from 'next/link'
import { useRouter } from 'next/navigation'
import { Activity, Eye, EyeOff, ArrowRight } from 'lucide-react'
import { Button } from '@/components/ui'
import { toast } from 'sonner'

export default function RegisterPage() {
  const router = useRouter()
  const [showPass, setShowPass] = useState(false)
  const [loading, setLoading] = useState(false)
  const [fullName, setFullName] = useState('')
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    setLoading(true)
    try {
      const res = await fetch(`${process.env.NEXT_PUBLIC_BASE_URL}/api/Auth/register`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          fullName,
          email,
          password,
        }),
      })

      const data = await res.json()
      console.log(data)
      if (res.ok) {
        localStorage.setItem('token', data.token)
        router.push('/dashboard/home')
        toast.success(data.message || 'Registration successful!')
      } else {
        toast.error(data.message || 'Failed to register.')
      }
    } catch (error) {
      toast.error('An error occurred during registration.')
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="animate-in">
      {/* Logo */}
      <div className="flex items-center justify-center gap-3 mb-8">
        <div
          className="w-9 h-9 rounded-xl flex items-center justify-center"
          style={{ background: 'var(--orange)' }}
        >
          <Activity size={18} color="#fff" strokeWidth={2.5} />
        </div>
        <span className="font-syne font-extrabold text-xl" style={{ color: 'var(--white)' }}>
          Xll<span style={{ color: 'var(--orange)' }}>er</span>ate
        </span>
      </div>

      <div
        className="rounded-2xl p-8"
        style={{ background: 'var(--bg-2)', border: '1px solid var(--border)' }}
      >
        <h1
          className="font-syne font-bold mb-1"
          style={{ fontSize: '22px', color: 'var(--white)' }}
        >
          Start your diagnosis
        </h1>
        <p style={{ fontSize: '13px', color: 'var(--white-3)', marginBottom: 24 }}>
          Find out exactly why you aren't getting hired
        </p>

        <form onSubmit={handleSubmit} className="space-y-4">
          <div>
            <label className="block mb-1.5 font-medium" style={{ fontSize: '12px', color: 'var(--white-2)' }}>
              Full name
            </label>
            <input 
              type="text" 
              placeholder="Aryan Kumar" 
              value={fullName}
              onChange={(e) => setFullName(e.target.value)}
              required 
            />
          </div>

          <div>
            <label className="block mb-1.5 font-medium" style={{ fontSize: '12px', color: 'var(--white-2)' }}>
              Email
            </label>
            <input 
              type="email" 
              placeholder="aryan@example.com" 
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              required 
            />
          </div>

          <div>
            <label className="block mb-1.5 font-medium" style={{ fontSize: '12px', color: 'var(--white-2)' }}>
              Password
            </label>
            <div className="relative">
              <input
                type={showPass ? 'text' : 'password'}
                placeholder="Min 8 characters"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                required
                style={{ paddingRight: '44px' }}
              />
              <button
                type="button"
                onClick={() => setShowPass(!showPass)}
                className="absolute right-3 top-1/2 -translate-y-1/2"
                style={{ color: 'var(--white-3)' }}
              >
                {showPass ? <EyeOff size={16} /> : <Eye size={16} />}
              </button>
            </div>
          </div>

          <Button
            type="submit"
            className="w-full justify-center font-syne font-bold text-[15px] transition-all hover:scale-[1.02] active:scale-95 group animate-pulse-glow"
            disabled={loading}
            style={{ 
              background: 'var(--orange)',
              opacity: loading ? 0.7 : 1, 
              marginTop: '16px',
              padding: '10px 0'
            }}
          >
            {loading ? 'Creating account…' : (
              <>
                Create account — it's free
                <ArrowRight size={16} className="ml-2 group-hover:translate-x-1 transition-transform" />
              </>
            )}
          </Button>
        </form>

        <p className="text-center mt-5" style={{ fontSize: '12px', color: 'var(--white-3)' }}>
          By signing up, you agree to our{' '}
          <Link href="#" style={{ color: 'var(--orange)' }}>Terms</Link>
          {' '}and{' '}
          <Link href="#" style={{ color: 'var(--orange)' }}>Privacy Policy</Link>
        </p>

        <p className="text-center mt-4" style={{ fontSize: '13px', color: 'var(--white-3)' }}>
          Already have an account?{' '}
          <Link href="/auth/login" style={{ color: 'var(--orange)', fontWeight: 500 }}>
            Sign in
          </Link>
        </p>
      </div>
    </div>
  )
}

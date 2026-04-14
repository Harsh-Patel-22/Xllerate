'use client'
import { cn } from '@/lib/utils'
import { forwardRef } from 'react'

/* ─── Button ─── */
interface ButtonProps extends React.ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: 'primary' | 'ghost' | 'danger'
  size?: 'sm' | 'md' | 'lg'
  icon?: React.ReactNode
}
export const Button = forwardRef<HTMLButtonElement, ButtonProps>(
  ({ variant = 'primary', size = 'md', icon, children, className, ...props }, ref) => {
    const base =
      'inline-flex items-center gap-1.5 font-medium rounded-lg transition-all duration-150 cursor-pointer border-0 font-dm'
    const sizes = {
      sm: 'px-3 py-1.5 text-xs',
      md: 'px-4 py-2 text-sm',
      lg: 'px-5 py-2.5 text-sm',
    }
    const variants = {
      primary:
        'text-white hover:opacity-90 active:scale-95',
      ghost:
        'border border-solid hover:opacity-80',
      danger: 'text-white hover:opacity-90',
    }
    const inlineStyles: React.CSSProperties =
      variant === 'primary'
        ? { background: 'var(--orange)' }
        : variant === 'ghost'
        ? {
            background: 'transparent',
            color: 'var(--white-2)',
            borderColor: 'var(--border-2)',
          }
        : { background: 'var(--red)' }

    return (
      <button
        ref={ref}
        style={inlineStyles}
        className={cn(base, sizes[size], variants[variant], className)}
        {...props}
      >
        {icon && <span className="flex-shrink-0">{icon}</span>}
        {children}
      </button>
    )
  }
)
Button.displayName = 'Button'

/* ─── Badge ─── */
interface BadgeProps {
  label: string
  bg?: string
  color?: string
  className?: string
}
export function Badge({ label, bg, color, className }: BadgeProps) {
  return (
    <span
      className={cn('inline-block rounded-full font-medium', className)}
      style={{
        background: bg ?? 'var(--bg-4)',
        color: color ?? 'var(--white-2)',
        fontSize: '11px',
        padding: '3px 10px',
      }}
    >
      {label}
    </span>
  )
}

/* ─── Card ─── */
interface CardProps {
  children: React.ReactNode
  className?: string
  style?: React.CSSProperties
}
export function Card({ children, className, style }: CardProps) {
  return (
    <div
      className={cn('card', className)}
      style={style}
    >
      {children}
    </div>
  )
}

export function CardHeader({
  children,
  className,
}: {
  children: React.ReactNode
  className?: string
}) {
  return (
    <div
      className={cn('flex items-center justify-between px-5 py-4', className)}
      style={{ borderBottom: '1px solid var(--border)' }}
    >
      {children}
    </div>
  )
}

export function CardTitle({ children }: { children: React.ReactNode }) {
  return (
    <span
      className="font-syne font-semibold"
      style={{ fontSize: '14px', color: 'var(--white)' }}
    >
      {children}
    </span>
  )
}

export function CardBody({
  children,
  className,
  style,
}: {
  children: React.ReactNode
  className?: string
  style?: React.CSSProperties
}) {
  return (
    <div className={cn('p-5', className)} style={style}>
      {children}
    </div>
  )
}

/* ─── StatCard ─── */
interface StatCardProps {
  label: string
  value: string | number
  sub?: string
  subType?: 'up' | 'down' | 'muted'
  valueColor?: string
  className?: string
}
export function StatCard({
  label,
  value,
  sub,
  subType = 'muted',
  valueColor,
  className,
}: StatCardProps) {
  const subColors = {
    up: 'var(--green)',
    down: 'var(--red)',
    muted: 'var(--white-3)',
  }
  return (
    <div
      className={cn('rounded-xl p-4', className)}
      style={{
        background: 'var(--bg-2)',
        border: '1px solid var(--border)',
      }}
    >
      <div
        className="font-semibold uppercase mb-2"
        style={{ fontSize: '10px', letterSpacing: '0.08em', color: 'var(--white-3)' }}
      >
        {label}
      </div>
      <div
        className="font-syne font-bold"
        style={{ fontSize: '26px', color: valueColor ?? 'var(--white)' }}
      >
        {value}
      </div>
      {sub && (
        <div
          className="mt-1"
          style={{ fontSize: '11px', color: subColors[subType] }}
        >
          {sub}
        </div>
      )}
    </div>
  )
}

/* ─── SkillBar ─── */
interface SkillBarProps {
  name: string
  confidence: number
  color?: string
  animDelay?: number
}
export function SkillBar({ name, confidence, color = 'var(--orange)', animDelay = 0 }: SkillBarProps) {
  return (
    <div className="mb-3">
      <div className="flex justify-between mb-1.5">
        <span style={{ fontSize: '12px', color: 'var(--white)', fontWeight: 500 }}>
          {name}
        </span>
        <span style={{ fontSize: '12px', color }}>
          {confidence}%
        </span>
      </div>
      <div
        className="h-1.5 rounded-full overflow-hidden"
        style={{ background: 'var(--bg-4)' }}
      >
        <div
          className="h-full rounded-full bar-animate"
          style={{
            width: `${confidence}%`,
            background: color,
            animationDelay: `${animDelay}ms`,
          }}
        />
      </div>
    </div>
  )
}

/* ─── Section Label ─── */
export function SectionLabel({
  children,
  color = 'var(--white-3)',
}: {
  children: React.ReactNode
  color?: string
}) {
  return (
    <div
      className="font-syne font-semibold uppercase mb-2.5"
      style={{ fontSize: '10px', letterSpacing: '0.1em', color }}
    >
      {children}
    </div>
  )
}

/* ─── InsightBox ─── */
export function InsightBox({ tag, text }: { tag: string; text: string }) {
  return (
    <div
      className="rounded-xl p-4 mb-3 last:mb-0"
      style={{
        background: 'var(--bg-3)',
        border: '1px solid var(--border)',
        borderLeft: '3px solid var(--orange)',
      }}
    >
      <div
        className="font-syne font-bold uppercase mb-1.5"
        style={{ fontSize: '10px', letterSpacing: '0.1em', color: 'var(--orange)' }}
      >
        {tag}
      </div>
      <p style={{ fontSize: '13px', color: 'var(--white-2)', lineHeight: 1.65 }}>
        {text}
      </p>
    </div>
  )
}

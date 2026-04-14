'use client'
import { useEffect, useState } from 'react'
import { getMatchLabel, ringOffset } from '@/lib/utils'

interface ScoreRingProps {
  score: number
  size?: number
  strokeWidth?: number
}

export default function ScoreRing({
  score,
  size = 160,
  strokeWidth = 12,
}: ScoreRingProps) {
  const radius = (size - strokeWidth * 2) / 2
  const circumference = 2 * Math.PI * radius
  const [animated, setAnimated] = useState(false)

  useEffect(() => {
    const t = setTimeout(() => setAnimated(true), 100)
    return () => clearTimeout(t)
  }, [])

  const offset = animated ? ringOffset(score, radius) : circumference

  let strokeColor = 'var(--orange)'
  if (score >= 80) strokeColor = 'var(--green)'
  else if (score < 60) strokeColor = 'var(--red)'

  const label = getMatchLabel(score)
  const cx = size / 2
  const cy = size / 2

  return (
    <div className="flex flex-col items-center">
      <div className="relative" style={{ width: size, height: size }}>
        <svg
          width={size}
          height={size}
          viewBox={`0 0 ${size} ${size}`}
          style={{ transform: 'rotate(-90deg)' }}
        >
          <circle
            cx={cx}
            cy={cy}
            r={radius}
            fill="none"
            stroke="var(--bg-4)"
            strokeWidth={strokeWidth}
          />
          <circle
            cx={cx}
            cy={cy}
            r={radius}
            fill="none"
            stroke={strokeColor}
            strokeWidth={strokeWidth}
            strokeLinecap="round"
            strokeDasharray={circumference}
            strokeDashoffset={offset}
            style={{ transition: 'stroke-dashoffset 1.2s cubic-bezier(0.4,0,0.2,1)' }}
          />
        </svg>
        <div
          className="absolute inset-0 flex flex-col items-center justify-center"
        >
          <span
            className="font-syne font-extrabold"
            style={{ fontSize: size > 120 ? '36px' : '24px', color: 'var(--white)', lineHeight: 1 }}
          >
            {score}
          </span>
          <span
            className="font-syne font-bold uppercase"
            style={{ fontSize: '10px', color: strokeColor, letterSpacing: '0.06em', marginTop: 2 }}
          >
            {label}
          </span>
        </div>
      </div>
      <div
        className="mt-3 rounded-full font-syne font-semibold"
        style={{
          background: 'var(--orange-dim)',
          color: 'var(--orange)',
          border: '1px solid var(--orange-glow)',
          fontSize: '12px',
          padding: '5px 16px',
        }}
      >
        {label} Candidate
      </div>
    </div>
  )
}

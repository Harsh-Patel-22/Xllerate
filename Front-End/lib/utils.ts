import { clsx, type ClassValue } from 'clsx'
import { twMerge } from 'tailwind-merge'

export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs))
}

export function getMatchLabel(score: number): string {
  if (score >= 80) return 'Strong'
  if (score >= 60) return 'Competitive'
  return 'At Risk'
}

export function getMatchColor(score: number): string {
  if (score >= 80) return 'var(--green)'
  if (score >= 60) return 'var(--orange)'
  return 'var(--red)'
}

export function getConfidenceColor(confidence: number): string {
  if (confidence >= 70) return 'var(--green)'
  if (confidence >= 35) return 'var(--amber)'
  return 'var(--red)'
}

export function getSeverityStyle(severity: string) {
  switch (severity) {
    case 'critical':
      return { bg: 'rgba(239,68,68,0.12)', color: '#FCA5A5', label: 'Critical' }
    case 'important':
      return { bg: 'rgba(245,158,11,0.12)', color: 'var(--amber)', label: 'Important' }
    default:
      return { bg: 'rgba(34,197,94,0.1)', color: '#86EFAC', label: 'Nice-to-have' }
  }
}

export function getGapTypeStyle(gapType: string) {
  switch (gapType) {
    case 'missing':
      return { bg: 'rgba(239,68,68,0.12)', color: '#FCA5A5', label: 'Missing' }
    case 'weak':
      return { bg: 'rgba(245,158,11,0.12)', color: '#FCD34D', label: 'Weak' }
    default:
      return { bg: 'rgba(148,163,184,0.12)', color: 'var(--white-3)', label: 'Unverified' }
  }
}

export function getRoadmapStatusColor(status: string) {
  switch (status) {
    case 'complete': return 'var(--green)'
    case 'active':   return 'var(--orange)'
    default:         return 'var(--border-2)'
  }
}

// Compute ring dashoffset for SVG ring (circumference = 2πr)
export function ringOffset(percent: number, radius = 65): number {
  const circumference = 2 * Math.PI * radius
  return circumference - (percent / 100) * circumference
}

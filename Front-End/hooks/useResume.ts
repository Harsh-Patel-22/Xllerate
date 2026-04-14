'use client'
import { useState, useEffect, useRef } from 'react'

type UploadPhase = 'idle' | 'uploading' | 'processing' | 'parsing' | 'complete' | 'error'

export interface UseResumeReturn {
  phase: UploadPhase
  progress: number
  filename: string
  error: string | null
  startUpload: (file: File) => void
  reset: () => void
}

/**
 * useResume — manages the resume upload + processing state machine.
 *
 * Currently simulates the backend polling with timeouts.
 * To connect to a real backend:
 *   1. In startUpload(), call resumeApi.upload(file) and store the resumeId.
 *   2. Start an interval calling resumeApi.getStatus(resumeId).
 *   3. Map the response status to UploadPhase and update state.
 *   4. Clear the interval when status === 'complete' or 'error'.
 */
export function useResume(): UseResumeReturn {
  const [phase, setPhase] = useState<UploadPhase>('idle')
  const [progress, setProgress] = useState(0)
  const [filename, setFilename] = useState('')
  const [error, setError] = useState<string | null>(null)
  const timerRef = useRef<ReturnType<typeof setTimeout> | null>(null)

  const clearTimer = () => {
    if (timerRef.current) clearTimeout(timerRef.current)
  }

  useEffect(() => () => clearTimer(), [])

  const startUpload = (file: File) => {
    clearTimer()
    setFilename(file.name)
    setError(null)
    setProgress(0)
    setPhase('uploading')

    const phases: { phase: UploadPhase; progress: number; delay: number }[] = [
      { phase: 'processing', progress: 30, delay: 700  },
      { phase: 'parsing',    progress: 65, delay: 1600 },
      { phase: 'complete',   progress: 100, delay: 2500 },
    ]

    phases.forEach(({ phase: p, progress: prog, delay }) => {
      timerRef.current = setTimeout(() => {
        setPhase(p)
        setProgress(prog)
      }, delay)
    })
  }

  const reset = () => {
    clearTimer()
    setPhase('idle')
    setProgress(0)
    setFilename('')
    setError(null)
  }

  return { phase, progress, filename, error, startUpload, reset }
}

'use client'
import { useCallback, useState } from 'react'
import { useDropzone } from 'react-dropzone'
import { Upload, CheckCircle2, Loader2, FileText, X } from 'lucide-react'
import { Button } from '@/components/ui'
import { toast } from 'sonner'

type UploadState = 'idle' | 'uploading' | 'processing' | 'parsing' | 'complete' | 'error'

const STEPS: { key: UploadState; label: string; sub: string }[] = [
  { key: 'uploading',   label: 'File uploaded',              sub: 'Transferred to server' },
  { key: 'processing', label: 'Extracting text',             sub: 'pages parsed' },
  { key: 'parsing',    label: 'Parsing skills & experience', sub: 'entities found' },
  { key: 'complete',   label: 'Profile complete',            sub: 'Ready to analyze' },
]

function stepIndex(state: UploadState): number {
  const order: UploadState[] = ['uploading', 'processing', 'parsing', 'complete']
  return order.indexOf(state)
}

export default function ResumeUploader({ onComplete }: { onComplete?: () => void }) {
  const [uploadState, setUploadState] = useState<UploadState>('idle')
  const [filename, setFilename] = useState('')
  const [progress, setProgress] = useState(0)

  const handleUpload = async (file: File) => {
    setFilename(file.name)
    setUploadState('uploading')
    setProgress(20)

    try {
      const token = localStorage.getItem('token')
      const formData = new FormData()
      formData.append('file', file)

      // Step 1: Upload the file
      const res = await fetch(`${process.env.NEXT_PUBLIC_BASE_URL}/api/Resume/upload`, {
        method: 'POST',
        headers: {
          'Authorization': `Bearer ${token}`
        },
        body: formData,
      })

      if (!res.ok) {
        throw new Error('Failed to upload file')
      }

      // Parse resumeId
      const textData = await res.text()
      let resumeId = ''
      try {
        const json = JSON.parse(textData)
        resumeId = json.resumeId || json.id || textData
        console.log(resumeId)
        console.log(json)
      } catch {
        // Assume raw string wrapped or clean
        resumeId = textData.trim()
        if (resumeId.startsWith('"') && resumeId.endsWith('"')) {
          resumeId = resumeId.substring(1, resumeId.length - 1)
        }
      }

      setUploadState('processing')
      setProgress(50)

      // Step 2: Set up a polling engine internally
      const pollStatus = async () => {
        try {
          const statusRes = await fetch(`${process.env.NEXT_PUBLIC_BASE_URL}/api/Resume/${resumeId}/status`, {
            headers: { 'Authorization': `Bearer ${token}` }
          })
          
          if (!statusRes.ok) {
            setUploadState('error')
            toast.error('Failed to parse resume status')
            return
          }

          const statusData = await statusRes.json()
          const statusVal = statusData.status ?? statusData
          
          if (statusVal === 1) {
            setUploadState('parsing')
            setProgress(80)
            
            // Trigger parallel enrichments safely in the background
            try {
              toast.info('Extracting signals from GitHub & LeetCode...')
              await Promise.allSettled([
                fetch(`${process.env.NEXT_PUBLIC_BASE_URL}/api/Enrichment/github`, {
                  method: 'POST',
                  headers: { 'Content-Type': 'application/json', 'Authorization': `Bearer ${token}` },
                  body: JSON.stringify({ username: "ha31457" }) // Hardcoded for now
                }),
                fetch(`${process.env.NEXT_PUBLIC_BASE_URL}/api/Enrichment/leetcode`, {
                  method: 'POST',
                  headers: { 'Content-Type': 'application/json', 'Authorization': `Bearer ${token}` },
                  body: JSON.stringify({ username: "harshpandya31457" }) // Hardcoded for now
                })
              ])
            } catch (enrichErr) {
              console.error("Enrichment failed:", enrichErr)
            }
            
            setTimeout(() => {
              setProgress(100)
              setUploadState('complete')
              toast.success('Resume parsed successfully!')
              onComplete?.()
            }, 500)
          } else {
            // Requeue the poll after 5 seconds if not yet 1 structurally
            setTimeout(pollStatus, 5000)
          }
        } catch (e) {
          setUploadState('error')
          toast.error('Network error during polling')
        }
      }

      // Initial execution kick-off for 5 seconds delay
      setTimeout(pollStatus, 5000)

    } catch (e) {
      setUploadState('error')
      toast.error('Network error during upload transfer')
    }
  }

  const onDrop = useCallback((accepted: File[]) => {
    if (accepted[0]) handleUpload(accepted[0])
  }, [])

  const { getRootProps, getInputProps, isDragActive } = useDropzone({
    onDrop,
    accept: { 'application/pdf': ['.pdf'] },
    maxSize: 10 * 1024 * 1024,
    multiple: false,
    disabled: uploadState !== 'idle',
  })

  const reset = () => {
    setUploadState('idle')
    setFilename('')
    setProgress(0)
  }

  const currentStep = stepIndex(uploadState)

  return (
    <div className="space-y-4">
      {/* Drop zone */}
      {uploadState === 'idle' ? (
        <div
          {...getRootProps()}
          className={`upload-zone ${isDragActive ? 'active' : ''}`}
        >
          <input {...getInputProps()} />
          <div
            className="w-14 h-14 rounded-xl flex items-center justify-center mx-auto mb-4"
            style={{ background: 'var(--bg-4)' }}
          >
            <Upload size={24} style={{ color: 'var(--orange)' }} />
          </div>
          <p
            className="font-syne font-semibold mb-1"
            style={{ fontSize: '16px', color: 'var(--white)' }}
          >
            Drop your resume here
          </p>
          <p style={{ fontSize: '13px', color: 'var(--white-3)' }}>
            Drag and drop, or click to browse
          </p>
          <span
            className="inline-block mt-3 rounded-full"
            style={{
              fontSize: '11px',
              color: 'var(--white-3)',
              background: 'var(--bg-4)',
              padding: '5px 14px',
            }}
          >
            PDF only · max 10 MB
          </span>
        </div>
      ) : (
        <div
          className="rounded-xl p-4"
          style={{ background: 'var(--bg-3)', border: '1px solid var(--border)' }}
        >
          <div className="flex items-center justify-between mb-3">
            <div className="flex items-center gap-2">
              <FileText size={16} style={{ color: 'var(--orange)' }} />
              <span style={{ fontSize: '13px', color: 'var(--white-2)' }}>{filename}</span>
            </div>
            <div className="flex items-center gap-2">
              {uploadState === 'complete' ? (
                <span
                  className="font-semibold"
                  style={{ fontSize: '12px', color: 'var(--green)' }}
                >
                  Complete
                </span>
              ) : (
                <Loader2 size={14} style={{ color: 'var(--orange)' }} className="animate-spin" />
              )}
              <button onClick={reset} style={{ color: 'var(--white-3)' }}>
                <X size={14} />
              </button>
            </div>
          </div>
          {/* Progress bar */}
          <div
            className="h-1.5 rounded-full overflow-hidden"
            style={{ background: 'var(--bg-4)' }}
          >
            <div
              className="h-full rounded-full transition-all duration-500"
              style={{
                width: `${progress}%`,
                background: uploadState === 'complete' ? 'var(--green)' : 'var(--orange)',
              }}
            />
          </div>
        </div>
      )}

      {/* Steps */}
      {uploadState !== 'idle' && (
        <div
          className="rounded-xl"
          style={{ background: 'var(--bg-3)', border: '1px solid var(--border)' }}
        >
          {STEPS.map((step, idx) => {
            const done = currentStep > idx
            const active = currentStep === idx
            return (
              <div
                key={step.key}
                className="flex gap-3 px-4 py-3"
                style={{
                  borderBottom: idx < STEPS.length - 1 ? '1px solid var(--border)' : 'none',
                }}
              >
                <div className="flex-shrink-0 mt-0.5">
                  {done ? (
                    <CheckCircle2 size={17} style={{ color: 'var(--green)' }} />
                  ) : active ? (
                    <Loader2 size={17} className="animate-spin" style={{ color: 'var(--orange)' }} />
                  ) : (
                    <div
                      className="w-4 h-4 rounded-full border flex items-center justify-center"
                      style={{
                        borderColor: 'var(--border-2)',
                        fontSize: '10px',
                        color: 'var(--white-3)',
                        fontFamily: 'var(--font-syne)',
                        fontWeight: 700,
                      }}
                    >
                      {idx + 1}
                    </div>
                  )}
                </div>
                <div>
                  <p
                    className="font-medium"
                    style={{
                      fontSize: '13px',
                      color: done || active ? 'var(--white)' : 'var(--white-3)',
                    }}
                  >
                    {step.label}
                  </p>
                  {(done || active) && (
                    <p
                      style={{
                        fontSize: '11px',
                        color: done ? 'var(--green)' : 'var(--orange)',
                        marginTop: 2,
                        fontWeight: 600,
                      }}
                    >
                      {done ? step.sub : 'In progress…'}
                    </p>
                  )}
                </div>
              </div>
            )
          })}
        </div>
      )}
    </div>
  )
}

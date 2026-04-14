'use client'
import { useState, useEffect } from 'react'
import TopBar from '@/components/layout/TopBar'
import { Button } from '@/components/ui'
import { MapPin, Briefcase, Banknote, Clock, Wifi } from 'lucide-react'

export interface JobFeedDto {
  id: string;
  title: string;
  company: string;
  companyInitial: string;
  companyColor?: string;
  location?: string;
  remote: boolean;
  salary?: string;
  experience?: string;
  skills: string[];
  missingSkills: string[];
  matchScore: number;
  scrapedAt?: string;
  jobUrl?: string;
}

const TABS = ['All Roles', 'Strong Match', 'Remote Only', 'Saved']


function JobCard({ job }: { job: JobFeedDto }) {
  const isGoodMatch = job.matchScore >= 80

  return (
    <div
      className="rounded-xl p-4 sm:p-5 cursor-pointer transition-all duration-150"
      style={{
        background: 'var(--bg-3)',
        border: '1px solid var(--border)',
      }}
      onClick={() => {
        if (job.jobUrl) {
          window.open(job.jobUrl, '_blank', 'noopener,noreferrer')
        }
      }}
      onMouseEnter={(e) => {
        ;(e.currentTarget as HTMLDivElement).style.borderColor = 'var(--border-2)'
        ;(e.currentTarget as HTMLDivElement).style.background = 'var(--bg-4)'
      }}
      onMouseLeave={(e) => {
        ;(e.currentTarget as HTMLDivElement).style.borderColor = 'var(--border)'
        ;(e.currentTarget as HTMLDivElement).style.background = 'var(--bg-3)'
      }}
    >
      {/* Header */}
      <div className="flex items-start justify-between gap-3 mb-3">
        <div className="flex items-center gap-3 min-w-0">
          <div
            className="w-9 h-9 rounded-lg flex items-center justify-center font-syne font-extrabold text-sm flex-shrink-0"
            style={{ background: job.companyColor || 'var(--bg-4)', color: '#1a1a1a' }}
          >
            {job.companyInitial}
          </div>
          <div className="min-w-0">
            <p style={{ fontSize: '11px', color: 'var(--white-3)' }}>{job.company}</p>
            <p
              className="font-syne font-semibold truncate"
              style={{ fontSize: '14px', color: 'var(--white)' }}
            >
              {job.title}
            </p>
          </div>
        </div>
        <span
          className="rounded-full font-syne font-bold flex-shrink-0"
          style={{
            fontSize: '12px',
            padding: '4px 12px',
            background: isGoodMatch ? 'rgba(34,197,94,0.12)' : 'var(--orange-dim)',
            color: isGoodMatch ? 'var(--green)' : 'var(--orange)',
          }}
        >
          {job.matchScore}% match
        </span>
      </div>

      {/* Skills */}
      <div className="flex flex-wrap gap-1.5 mb-3">
        {job.skills.map((s) => (
          <span
            key={s}
            className="rounded"
            style={{
              fontSize: '11px',
              padding: '3px 9px',
              background: 'var(--bg-5)',
              color: 'var(--white-2)',
            }}
          >
            {s}
          </span>
        ))}
        {job.missingSkills.map((s) => (
          <span
            key={s}
            className="rounded font-medium"
            style={{
              fontSize: '11px',
              padding: '3px 9px',
              background: 'rgba(239,68,68,0.08)',
              color: '#FCA5A5',
              border: '1px solid rgba(239,68,68,0.15)',
            }}
          >
            {s} · missing
          </span>
        ))}
      </div>

      {/* Footer */}
      <div
        className="flex flex-wrap items-center gap-3 pt-3"
        style={{ borderTop: '1px solid var(--border)' }}
      >
        <div className="flex items-center gap-1.5" style={{ fontSize: '11px', color: 'var(--white-3)' }}>
          <MapPin size={12} />
          {job.location}
          {job.remote && (
            <span className="ml-1 flex items-center gap-1">
              <Wifi size={11} />
              Remote OK
            </span>
          )}
        </div>
        <div className="flex items-center gap-1.5" style={{ fontSize: '11px', color: 'var(--white-3)' }}>
          <Briefcase size={12} />
          {job.experience}
        </div>
        <div className="flex items-center gap-1.5" style={{ fontSize: '11px', color: 'var(--white-3)' }}>
          <Banknote size={12} />
          {job.salary}
        </div>
        <div className="flex items-center gap-1.5 ml-auto" style={{ fontSize: '11px', color: 'var(--white-3)' }}>
          <Clock size={12} />
          {job.scrapedAt || 'Recently'}
        </div>
        <Button
          variant={isGoodMatch ? 'primary' : 'ghost'}
          size="sm"
          style={{ padding: '5px 14px', fontSize: '12px' }}
          onClick={(e) => {
            e.stopPropagation()
            if (isGoodMatch && job.jobUrl) {
              window.open(job.jobUrl, '_blank', 'noopener,noreferrer')
            }
          }}
        >
          {isGoodMatch ? 'Apply' : 'Save'}
        </Button>
      </div>
    </div>
  )
}

export default function JobsPage() {
  const [activeTab, setActiveTab] = useState('All Roles')
  const [search, setSearch] = useState('')
  const [jobs, setJobs] = useState<JobFeedDto[]>([])
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    const fetchJobs = async () => {
      try {
        const token = localStorage.getItem('token')
        const res = await fetch(`${process.env.NEXT_PUBLIC_BASE_URL}/api/user/feed?limit=50`, {
          method: 'GET',
          headers: {
            'Authorization': `Bearer ${token}`,
          },
        })
        if (res.ok) {
          const data = await res.json()
          console.log(data)
          setJobs(data)
        }
      } catch (err) {
        console.error('Failed to fetch jobs:', err)
      } finally {
        setLoading(false)
      }
    }
    fetchJobs()
  }, [])
  

  const filtered = jobs.filter((job) => {
    const matchesSearch =
      !search ||
      job.title.toLowerCase().includes(search.toLowerCase()) ||
      job.company.toLowerCase().includes(search.toLowerCase())
    if (activeTab === 'Strong Match') return matchesSearch && job.matchScore >= 80
    if (activeTab === 'Remote Only') return matchesSearch && job.remote
    return matchesSearch
  })

  return (
    <div className="animate-in">
      <TopBar
        title="Jobs"
        subtitle={`Matched to your profile · ${jobs.length} openings`}
      />

      <div className="px-4 sm:px-6 lg:px-8 py-6 space-y-4">
        {/* Search */}
        <div className="animate-in animate-in-delay-1">
          <input
            type="text"
            placeholder="Search roles or companies…"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            style={{ maxWidth: '400px' }}
          />
        </div>

        {/* Tabs */}
        <div
          className="flex gap-1 p-1 rounded-xl w-full sm:w-auto animate-in animate-in-delay-2"
          style={{ background: 'var(--bg-3)', display: 'inline-flex' }}
        >
          {TABS.map((tab) => (
            <button
              key={tab}
              onClick={() => setActiveTab(tab)}
              className="flex-1 sm:flex-none rounded-lg transition-all duration-150 whitespace-nowrap"
              style={{
                padding: '7px 14px',
                fontSize: '12px',
                fontWeight: 500,
                color: activeTab === tab ? 'var(--white)' : 'var(--white-3)',
                background: activeTab === tab ? 'var(--bg-2)' : 'transparent',
                fontFamily: 'var(--font-dm-sans)',
                cursor: 'pointer',
                border: 'none',
              }}
            >
              {tab}
            </button>
          ))}
        </div>

        {/* Job list */}
        <div className="space-y-3 animate-in animate-in-delay-3">
          {loading ? (
            <div className="flex justify-center p-8 text-white-3 font-medium text-sm">
              Loading jobs...
            </div>
          ) : filtered.length > 0 ? (
            filtered.map((job) => <JobCard key={job.id} job={job} />)
          ) : (
            <div
              className="flex flex-col items-center py-16 rounded-xl"
              style={{ border: '1px solid var(--border)', background: 'var(--bg-2)' }}
            >
              <Briefcase size={32} style={{ color: 'var(--white-3)', marginBottom: 12 }} />
              <p style={{ fontSize: '14px', color: 'var(--white-3)' }}>
                No jobs found for this filter.
              </p>
            </div>
          )}
        </div>
      </div>
    </div>
  )
}

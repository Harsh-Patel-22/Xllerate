'use client'
import TopBar from '@/components/layout/TopBar'
import {
  Card, CardHeader, CardTitle, CardBody, SectionLabel, Button, StatCard,
} from '@/components/ui'
import { mockUser, mockResume, mockAnalysis } from '@/lib/mock-data'
import { Github, CheckCircle2, XCircle, Edit3, ExternalLink } from 'lucide-react'
import { useState, useEffect } from 'react'
import { toast } from 'sonner'

interface RoleDto {
  roleKey: string;
  displayName: string;
  jobsSampled: number;
  confidence: number;
}

const SOURCES: { type: string; icon: string; desc: string; connected: boolean; stats?: string }[] = [
  {
    type: 'GitHub',
    icon: '⌥',
    desc: '34 repos · 4 languages · 320 stars',
    connected: true,
    stats: 'github.com/aryan-k',
  },
  {
    type: 'LeetCode',
    icon: '⌘',
    desc: 'Boost your algorithm confidence score',
    connected: false,
  },
  {
    type: 'LinkedIn',
    icon: '☁',
    desc: 'Cross-verify work history',
    connected: false,
  },
]

export default function ProfilePage() {
  const user = mockUser
  const resume = mockResume
  const analysis = mockAnalysis

  const [selectedRoleKey, setSelectedRoleKey] = useState('')
  const [isEditingRole, setIsEditingRole] = useState(false)
  const [isSavingRole, setIsSavingRole] = useState(false)
  const [availableRoles, setAvailableRoles] = useState<RoleDto[]>([])

  useEffect(() => {
    const fetchRolesData = async () => {
      try {
        const token = localStorage.getItem('token')
        const headers = { 'Authorization': `Bearer ${token}` }

        // Fetch all possible roles
        const rolesRes = await fetch(`${process.env.NEXT_PUBLIC_BASE_URL}/api/jobs/role-name`, { headers })
        if (rolesRes.ok) {
          const data = await rolesRes.json()
          setAvailableRoles(data)
        }

        // Fetch current user's role
        const userRoleRes = await fetch(`${process.env.NEXT_PUBLIC_BASE_URL}/api/user/role`, { headers })
        if (userRoleRes.ok) {
          const text = await userRoleRes.text()
          if (text) {
            try {
              const data = JSON.parse(text)
              setSelectedRoleKey(data.roleKey || data.role || '')
            } catch {
              // If it's a plain string
              setSelectedRoleKey(text.trim())
            }
          }
        }
      } catch (e) {
        console.error("Failed to fetch roles data", e)
      }
    }
    fetchRolesData()
  }, [])

  const handleSaveRole = async () => {
    if (!selectedRoleKey.trim()) return;
    setIsSavingRole(true)
    try {
      const token = localStorage.getItem('token')
      console.log("selectedRoleKey", selectedRoleKey)
      const res = await fetch(`${process.env.NEXT_PUBLIC_BASE_URL}/api/user/role/${selectedRoleKey}`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'Authorization': `Bearer ${token}`
        }
        // body: JSON.stringify({ roleKey: selectedRoleKey })
      })
      if (res.ok) {
        toast.success('Desired role updated successfully!')
        setIsEditingRole(false)
      } else {
        const errData = await res.json().catch(() => ({}))
        toast.error(errData.message || 'Failed to update desired role')
      }
    } catch (e) {
      toast.error('An error occurred while updating the role')
    } finally {
      setIsSavingRole(false)
    }
  }

  const initials = user.name
    .split(' ')
    .map((n) => n[0])
    .join('')

  const allSkills = [
    ...analysis.strengths.map((s) => ({ ...s, type: 'strong' })),
    ...analysis.weakSkills.map((s) => ({ ...s, type: 'weak' })),
    ...analysis.missingSkills.map((s) => ({ ...s, type: 'missing' })),
  ]

  return (
    <div className="animate-in">
      <TopBar
        title="Profile"
        subtitle="Your career identity across all sources"
      />

      <div className="px-4 sm:px-6 lg:px-8 py-6 space-y-5">
        {/* User hero + enrichment */}
        <div className="grid grid-cols-1 lg:grid-cols-2 gap-5 animate-in animate-in-delay-1">
          {/* User card */}
          <Card>
            <CardBody style={{ padding: '24px' }}>
              <div className="flex items-start gap-4">
                <div
                  className="w-16 h-16 rounded-full flex items-center justify-center font-syne font-extrabold text-xl text-white flex-shrink-0"
                  style={{
                    background: 'linear-gradient(135deg, var(--orange), #FF9560)',
                  }}
                >
                  {initials}
                </div>
                <div className="flex-1 min-w-0">
                  <h2
                    className="font-syne font-bold"
                    style={{ fontSize: '20px', color: 'var(--white)' }}
                  >
                    {user.name}
                  </h2>
                  <p style={{ fontSize: '13px', color: 'var(--white-3)', marginTop: 2 }}>
                    {user.email}
                  </p>
                  <div className="flex flex-wrap gap-2 mt-3">
                    <span
                      className="rounded-full font-semibold capitalize"
                      style={{
                        fontSize: '11px',
                        padding: '4px 12px',
                        background: 'var(--orange-dim)',
                        color: 'var(--orange)',
                      }}
                    >
                      {user.plan} Plan
                    </span>
                    <span
                      className="rounded-full font-semibold"
                      style={{
                        fontSize: '11px',
                        padding: '4px 12px',
                        background: 'rgba(34,197,94,0.1)',
                        color: 'var(--green)',
                      }}
                    >
                      Profile {user.profileCompletion}% complete
                    </span>
                  </div>
                </div>
              </div>

              {/* Completion bar */}
              <div className="mt-5">
                <div className="flex justify-between mb-1.5">
                  <span style={{ fontSize: '11px', color: 'var(--white-3)' }}>
                    Profile completeness
                  </span>
                  <span style={{ fontSize: '11px', color: 'var(--orange)', fontWeight: 600 }}>
                    {user.profileCompletion}%
                  </span>
                </div>
                <div
                  className="h-1.5 rounded-full overflow-hidden"
                  style={{ background: 'var(--bg-4)' }}
                >
                  <div
                    className="h-full rounded-full bar-animate"
                    style={{ width: `${user.profileCompletion}%`, background: 'var(--orange)' }}
                  />
                </div>
                <p
                  className="mt-2"
                  style={{ fontSize: '11px', color: 'var(--white-3)' }}
                >
                  Connect LeetCode and LinkedIn to reach 100%
                </p>
              </div>

              {/* Target Role Editor */}
              <div className="mt-5 pt-5" style={{ borderTop: '1px solid var(--border)' }}>
                <div className="flex justify-between items-center mb-2">
                  <span style={{ fontSize: '12px', color: 'var(--white-2)', fontWeight: 500 }}>
                    Target Role
                  </span>
                  {!isEditingRole && (
                    <button onClick={() => setIsEditingRole(true)} style={{ fontSize: '11px', color: 'var(--orange)' }} className="flex items-center gap-1 hover:underline">
                      <Edit3 size={11} /> Edit
                    </button>
                  )}
                </div>
                
                {isEditingRole ? (
                  <div className="flex gap-2">
                    <select
                      value={selectedRoleKey} 
                      onChange={e => setSelectedRoleKey(e.target.value)}
                      style={{ flex: 1, padding: '6px 10px', fontSize: '12px', background: 'var(--bg-4)', border: '1px solid var(--border)', borderRadius: '6px', color: 'white', outline: 'none' }}
                      autoFocus
                    >
                      <option value="" disabled>Select a target role</option>
                      {availableRoles.map(role => (
                        <option key={role.roleKey} value={role.roleKey}>
                          {role.displayName}
                        </option>
                      ))}
                    </select>
                    <Button 
                      size="sm" 
                      onClick={handleSaveRole} 
                      disabled={isSavingRole}
                      style={{ padding: '6px 12px', fontSize: '12px', background: 'var(--orange)', color: 'white' }}
                    >
                      {isSavingRole ? 'Saving...' : 'Save'}
                    </Button>
                    <Button 
                      size="sm" 
                      variant="ghost"
                      onClick={() => setIsEditingRole(false)} 
                      disabled={isSavingRole}
                      style={{ padding: '6px 12px', fontSize: '12px' }}
                    >
                      Cancel
                    </Button>
                  </div>
                ) : (
                  <p style={{ fontSize: '14px', color: 'var(--white)', fontWeight: 500 }}>
                    {availableRoles.find(r => r.roleKey === selectedRoleKey)?.displayName || selectedRoleKey || 'Not set'}
                  </p>
                )}
                <p className="mt-1.5" style={{ fontSize: '11px', color: 'var(--white-3)' }}>
                  We use this role to generate your match scores and skill gaps.
                </p>
              </div>
            </CardBody>
          </Card>

          {/* Source enrichment */}
          <Card>
            <CardHeader>
              <CardTitle>Source Enrichment</CardTitle>
            </CardHeader>
            <CardBody style={{ padding: '4px 20px' }}>
              {SOURCES.map((source, idx) => (
                <div
                  key={source.type}
                  className="flex items-center justify-between py-3.5"
                  style={{
                    borderBottom: idx < SOURCES.length - 1 ? '1px solid var(--border)' : 'none',
                  }}
                >
                  <div className="flex items-center gap-3">
                    <div
                      className="w-9 h-9 rounded-lg flex items-center justify-center text-base flex-shrink-0"
                      style={{ background: 'var(--bg-4)' }}
                    >
                      {source.icon}
                    </div>
                    <div>
                      <p
                        className="font-semibold"
                        style={{ fontSize: '13px', color: 'var(--white)' }}
                      >
                        {source.type}
                      </p>
                      <p style={{ fontSize: '11px', color: 'var(--white-3)', marginTop: 1 }}>
                        {source.connected ? source.stats : source.desc}
                      </p>
                    </div>
                  </div>
                  {source.connected ? (
                    <span
                      className="rounded-full font-semibold"
                      style={{
                        fontSize: '11px',
                        padding: '4px 12px',
                        background: 'rgba(34,197,94,0.12)',
                        color: 'var(--green)',
                      }}
                    >
                      Connected
                    </span>
                  ) : (
                    <Button variant="ghost" size="sm">Connect</Button>
                  )}
                </div>
              ))}
            </CardBody>
          </Card>
        </div>

        {/* Stats row */}
        <div className="grid grid-cols-2 sm:grid-cols-4 gap-3 animate-in animate-in-delay-2">
          <StatCard label="Match Score" value="72%" valueColor="var(--orange)" sub="Backend Engineer" />
          <StatCard label="Skills Verified" value="14" sub="+3 via GitHub" subType="up" />
          <StatCard label="Resumes" value="1" sub="Active" />
          <StatCard label="Roadmap" value="28%" sub="In progress" subType="up" valueColor="var(--orange)" />
        </div>

        {/* All verified skills */}
        <Card className="animate-in animate-in-delay-3">
          <CardHeader>
            <CardTitle>All Verified Skills</CardTitle>
            <span style={{ fontSize: '12px', color: 'var(--white-3)' }}>
              Confidence from all sources
            </span>
          </CardHeader>
          <CardBody>
            <div className="flex flex-wrap gap-2">
              {allSkills.map((skill) => {
                const colors =
                  skill.type === 'strong'
                    ? { bg: 'rgba(34,197,94,0.1)', color: 'var(--green)', border: 'rgba(34,197,94,0.2)' }
                    : skill.type === 'weak'
                    ? { bg: 'rgba(245,158,11,0.1)', color: 'var(--amber)', border: 'rgba(245,158,11,0.2)' }
                    : { bg: 'rgba(239,68,68,0.08)', color: '#FCA5A5', border: 'rgba(239,68,68,0.15)' }
                return (
                  <span
                    key={skill.name}
                    className="rounded-lg font-medium"
                    style={{
                      fontSize: '12px',
                      padding: '6px 12px',
                      background: colors.bg,
                      color: colors.color,
                      border: `1px solid ${colors.border}`,
                    }}
                  >
                    {skill.name}
                    {skill.confidence > 0 ? ` · ${skill.confidence}%` : ''}
                  </span>
                )
              })}
            </div>
          </CardBody>
        </Card>

        {/* Experience */}
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-5 animate-in animate-in-delay-4">
          <Card>
            <CardHeader><CardTitle>Experience</CardTitle></CardHeader>
            <CardBody style={{ padding: '8px 20px' }}>
              {resume.experience?.map((exp, i) => (
                <div
                  key={`${exp.role}-${i}`}
                  className="py-3.5"
                  style={{
                    borderBottom: i < (resume.experience?.length ?? 0) - 1 ? '1px solid var(--border)' : 'none',
                  }}
                >
                  <p className="font-semibold" style={{ fontSize: '13px', color: 'var(--white)' }}>
                    {exp.role}
                  </p>
                  <p style={{ fontSize: '12px', color: 'var(--orange)', marginTop: 2, fontWeight: 500 }}>
                    {exp.company}
                  </p>
                  <p style={{ fontSize: '11px', color: 'var(--white-3)', marginTop: 2 }}>
                    {exp.period}
                  </p>
                </div>
              ))}
            </CardBody>
          </Card>

          <Card>
            <CardHeader><CardTitle>Education</CardTitle></CardHeader>
            <CardBody>
              {resume.education?.map((edu) => (
                <div
                  key={edu.institution}
                  className="rounded-xl p-4"
                  style={{ background: 'var(--bg-3)', border: '1px solid var(--border)' }}
                >
                  <p className="font-semibold" style={{ fontSize: '14px', color: 'var(--white)' }}>
                    {edu.degree}
                  </p>
                  <p style={{ fontSize: '12px', color: 'var(--white-3)', marginTop: 3 }}>
                    {edu.institution} · {edu.period}
                  </p>
                  {edu.gpa && (
                    <p className="font-semibold mt-2" style={{ fontSize: '13px', color: 'var(--orange)' }}>
                      CGPA {edu.gpa}
                    </p>
                  )}
                </div>
              ))}
            </CardBody>
          </Card>
        </div>
      </div>
    </div>
  )
}

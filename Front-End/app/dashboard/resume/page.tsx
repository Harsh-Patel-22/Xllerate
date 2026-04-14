'use client'
import TopBar from '@/components/layout/TopBar'
import { Card, CardHeader, CardTitle, CardBody, SectionLabel, Button } from '@/components/ui'
import ResumeUploader from '@/components/resume/ResumeUploader'
import { CheckCircle2, Code2 } from 'lucide-react'
import Link from 'next/link'
import { useState, useEffect } from 'react'

export interface ResumeSkillDto {
  name: string;
  confidence: number;
}

export interface ResumeEducationDto {
  degree?: string;
  institution?: string;
  period?: string;
  gpa?: string;
}

export interface ResumeExperienceDto {
  role?: string;
  company?: string;
  period?: string;
}

export interface ResumeDto {
  id: string;
  filename?: string;
  uploadedAt?: string;
  status: string;
  skills: ResumeSkillDto[];
  education: ResumeEducationDto[];
  experience: ResumeExperienceDto[];
}

export default function ResumePage() {
  const [resume, setResume] = useState<ResumeDto | null>(null)
  const [loading, setLoading] = useState(true)

  const fetchResume = async () => {
    try {
      const token = localStorage.getItem('token')
      const res = await fetch(`${process.env.NEXT_PUBLIC_BASE_URL}/api/Resume`, {
        method: 'GET',
        headers: { 'Authorization': `Bearer ${token}` }
      })
      if (res.ok) {
        const data = await res.json()
        setResume(data)
      }
    } catch (err) {
      console.error('Failed to fetch resume:', err)
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    fetchResume()
  }, [])

  return (
    <div className="animate-in">
      <TopBar
        title="Resume"
        subtitle="Upload and manage your resume files"
        action={
          <Link href="/dashboard/analysis">
            <Button variant="ghost" size="sm">Run Analysis →</Button>
          </Link>
        }
      />

      <div className="px-4 sm:px-6 lg:px-8 py-6 space-y-5">
        {/* Upload + Processing */}
        <div className="grid grid-cols-1 lg:grid-cols-2 gap-5 animate-in animate-in-delay-1">
          <Card>
            <CardHeader>
              <CardTitle>Upload New Resume</CardTitle>
            </CardHeader>
            <CardBody>
              <ResumeUploader onComplete={fetchResume} />
            </CardBody>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle>Current Resume</CardTitle>
              {resume && (
                <span
                  className="font-semibold rounded-full"
                  style={{
                    fontSize: '11px',
                    color: 'var(--green)',
                    background: 'rgba(34,197,94,0.12)',
                    padding: '3px 10px',
                  }}
                >
                  Active
                </span>
              )}
            </CardHeader>
            <CardBody>
              {loading ? (
                <p style={{ color: 'var(--white-3)', fontSize: '13px' }}>Loading resume details...</p>
              ) : resume ? (
                <div
                  className="flex items-center gap-3 p-3 rounded-xl mb-4"
                  style={{ background: 'var(--bg-3)', border: '1px solid var(--border)' }}
                >
                  <div
                    className="w-10 h-10 rounded-lg flex items-center justify-center flex-shrink-0"
                    style={{ background: 'var(--orange-dim)' }}
                  >
                    <Code2 size={18} style={{ color: 'var(--orange)' }} />
                  </div>
                  <div className="flex-1 min-w-0">
                    <p
                      className="font-medium truncate"
                      style={{ fontSize: '13px', color: 'var(--white)' }}
                    >
                      {resume.filename || 'Unknown Document'}
                    </p>
                    <p style={{ fontSize: '11px', color: 'var(--white-3)', marginTop: 2 }}>
                      Uploaded {resume.uploadedAt || 'Recently'}
                    </p>
                  </div>
                  <CheckCircle2 size={18} style={{ color: 'var(--green)', flexShrink: 0 }} />
                </div>
              ) : (
                <p style={{ color: 'var(--white-3)', fontSize: '13px' }}>No resume uploaded yet.</p>
              )}
            </CardBody>
          </Card>
        </div>

        {/* Extracted Profile */}
        {resume && !loading && (
          <Card className="animate-in animate-in-delay-2">
            <CardHeader>
              <CardTitle>Extracted Profile</CardTitle>
              <span style={{ fontSize: '12px', color: 'var(--white-3)' }}>
                {resume.filename}
              </span>
            </CardHeader>
            <CardBody>
              <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-6">
                {/* Education */}
                <div>
                  <SectionLabel>Education</SectionLabel>
                  {resume.education?.length > 0 ? resume.education.map((edu, idx) => (
                    <div
                      key={idx}
                      className="rounded-xl p-4 mb-2"
                      style={{ background: 'var(--bg-3)', border: '1px solid var(--border)' }}
                    >
                      <p
                        className="font-semibold mb-1"
                        style={{ fontSize: '13px', color: 'var(--white)' }}
                      >
                        {edu.degree}
                      </p>
                      <p style={{ fontSize: '12px', color: 'var(--white-3)' }}>
                        {edu.institution} · {edu.period}
                      </p>
                      {edu.gpa && (
                        <p
                          className="font-semibold mt-2"
                          style={{ fontSize: '12px', color: 'var(--orange)' }}
                        >
                          CGPA {edu.gpa}
                        </p>
                      )}
                    </div>
                  )) : (
                    <p style={{ fontSize: '12px', color: 'var(--white-3)' }}>No education history found.</p>
                  )}
                </div>

                {/* Experience */}
                <div>
                  <SectionLabel>Experience</SectionLabel>
                  <div className="space-y-2">
                    {resume.experience?.length > 0 ? resume.experience.map((exp, idx) => (
                      <div
                        key={idx}
                        className="rounded-xl p-3"
                        style={{ background: 'var(--bg-3)', border: '1px solid var(--border)' }}
                      >
                        <p
                          className="font-semibold"
                          style={{ fontSize: '13px', color: 'var(--white)' }}
                        >
                          {exp.role}
                        </p>
                        <p style={{ fontSize: '12px', color: 'var(--white-3)', marginTop: 2 }}>
                          {exp.company}
                        </p>
                        <p
                          style={{ fontSize: '11px', color: 'var(--white-3)', marginTop: 2 }}
                        >
                          {exp.period}
                        </p>
                      </div>
                    )) : (
                      <p style={{ fontSize: '12px', color: 'var(--white-3)' }}>No work experience found.</p>
                    )}
                  </div>
                </div>

                {/* Skills extracted */}
                <div>
                  <SectionLabel>Skills Detected</SectionLabel>
                  <div className="flex flex-wrap gap-2">
                    {resume.skills?.length > 0 ? resume.skills.slice(0, 15).map((skill, idx) => (
                      <span
                        key={idx}
                        className="rounded-lg font-medium"
                        style={{
                          fontSize: '11px',
                          padding: '4px 10px',
                          background:
                            skill.confidence >= 70
                              ? 'rgba(34,197,94,0.1)'
                              : skill.confidence >= 40
                              ? 'rgba(245,158,11,0.1)'
                              : 'var(--bg-4)',
                          color:
                            skill.confidence >= 70
                              ? 'var(--green)'
                              : skill.confidence >= 40
                              ? 'var(--amber)'
                              : 'var(--white-2)',
                          border: '1px solid',
                          borderColor:
                            skill.confidence >= 70
                              ? 'rgba(34,197,94,0.2)'
                              : skill.confidence >= 40
                              ? 'rgba(245,158,11,0.2)'
                              : 'var(--border)',
                        }}
                      >
                        {skill.name} · {skill.confidence}%
                      </span>
                    )) : (
                      <p style={{ fontSize: '12px', color: 'var(--white-3)' }}>No skills extracted.</p>
                    )}
                  </div>
                </div>
              </div>
            </CardBody>
          </Card>
        )}
      </div>
    </div>
  )
}

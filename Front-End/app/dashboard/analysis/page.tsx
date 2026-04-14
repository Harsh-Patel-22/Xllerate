'use client'
import TopBar from '@/components/layout/TopBar'
import {
  Card, CardHeader, CardTitle, CardBody,
  InsightBox, Button,
  SectionLabel,
} from '@/components/ui'
import ScoreRing from '@/components/ui/ScoreRing'
import { getSeverityStyle, getGapTypeStyle } from '@/lib/utils'
import { RefreshCw, Briefcase } from 'lucide-react'
import { useState, useEffect } from 'react'

export interface SkillGapDto {
  skillName: string;
  gapType: string;
  severity: string;
  userScore: number;
  requiredScore: number;
  scoreGap: number;
  recommendation: string | null;
}

export interface SkillSummaryDto {
  totalRequired: number;
  matched: number;
  missing: number;
  weak: number;
  unverified: number;
}

export interface AnalysisInsightDto {
  summary?: string;
  whyNotHired?: string;
  strengths: string[];
  criticalGaps: string[];
  marketContext?: string;
  geminiRawResponse?: string;
}

export interface AnalysisResultDto {
  analysisId: string;
  jobTitle: string;
  companyName: string;
  matchPercentage: number;
  status: string;
  createdAt: string;
  skillGaps: SkillGapDto[];
  summary: SkillSummaryDto;
  insightData?: AnalysisInsightDto;
}

export default function AnalysisPage() {
  const [analysis, setAnalysis] = useState<AnalysisResultDto | null>(null)
  const [loading, setLoading] = useState(true)

  const fetchAnalysis = async () => {
    setLoading(true)
    try {
      const token = localStorage.getItem('token')
      const res = await fetch(`${process.env.NEXT_PUBLIC_BASE_URL}/api/analysis/role`, {
          method: 'GET',
          headers: {
            'Authorization': `Bearer ${token}`,
          },
        })
      if (res.ok) {
        const data = await res.json()
        console.log("Fetched analysis data:", data)
        setAnalysis(Array.isArray(data) ? data[0] : data)
      }
    } catch (err) {
      console.error('Failed to fetch analysis:', err)
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    fetchAnalysis()
  }, [])

  if (loading) {
    return (
      <div className="flex flex-col items-center py-16">
        <p style={{ color: 'var(--white-3)' }}>Loading analysis...</p>
      </div>
    )
  }

  if (!analysis) {
    return (
      <div className="flex flex-col items-center py-16">
        <Briefcase size={32} style={{ color: 'var(--white-3)', marginBottom: 12 }} />
        <p style={{ color: 'var(--white-3)' }}>No analysis results found.</p>
      </div>
    )
  }

  const roleInfo = analysis.companyName ? `${analysis.jobTitle}` : analysis.jobTitle

  return (
    <div className="animate-in">
      <TopBar
        title="Analysis"
        subtitle={`Target role: ${roleInfo}`}
      />

      <div className="px-4 sm:px-6 lg:px-8 py-6 space-y-5">

        {/* Score + Gaps */}
        <div className="grid grid-cols-1 lg:grid-cols-3 gap-5 animate-in animate-in-delay-1">
          {/* Score ring */}
          <Card className="flex flex-col items-center justify-center py-6">
            <ScoreRing score={Math.round(analysis.matchPercentage)} />
            <p
              className="mt-4 text-center px-6"
              style={{ fontSize: '12px', color: 'var(--white-3)', lineHeight: 1.6 }}
            >
              Fix critical gaps to reach{' '}
              <span style={{ color: 'var(--orange)', fontWeight: 600 }}>
                Strong Match (85%+)
              </span>
            </p>
          </Card>

          {/* Gaps table */}
          <Card className="lg:col-span-2">
            <CardHeader>
              <CardTitle>Skill Gaps</CardTitle>
              <span style={{ fontSize: '12px', color: 'var(--white-3)' }}>
                {analysis.skillGaps?.length} gaps found
              </span>
            </CardHeader>
            <div className="overflow-x-auto">
              <table className="w-full" style={{ minWidth: '420px' }}>
                <thead>
                  <tr style={{ borderBottom: '1px solid var(--border)' }}>
                    {['Skill', 'Type', 'Severity', 'Score Gap'].map((h) => (
                      <th
                        key={h}
                        className="text-left font-semibold uppercase"
                        style={{
                          padding: '10px 16px',
                          fontSize: '10px',
                          letterSpacing: '0.08em',
                          color: 'var(--white-3)',
                        }}
                      >
                        {h}
                      </th>
                    ))}
                  </tr>
                </thead>
                <tbody>
                  {analysis.skillGaps?.map((gap, i) => {
                    const gt = getGapTypeStyle(gap.gapType)
                    const sv = getSeverityStyle(gap.severity)
                    return (
                      <tr
                        key={gap.skillName}
                        style={{
                          borderBottom: i < analysis.skillGaps?.length - 1 ? '1px solid var(--border)' : 'none',
                        }}
                      >
                        <td
                          className="font-medium"
                          style={{ padding: '12px 16px', fontSize: '13px', color: 'var(--white)' }}
                        >
                          {gap.skillName}
                        </td>
                        <td style={{ padding: '12px 16px' }}>
                          <span
                            className="rounded font-medium"
                            style={{
                              fontSize: '11px',
                              padding: '3px 10px',
                              background: gt.bg,
                              color: gt.color,
                            }}
                          >
                            {gt.label}
                          </span>
                        </td>
                        <td style={{ padding: '12px 16px' }}>
                          <span
                            className="rounded font-medium"
                            style={{
                              fontSize: '11px',
                              padding: '3px 10px',
                              background: sv.bg,
                              color: sv.color,
                            }}
                          >
                            {sv.label}
                          </span>
                        </td>
                        <td style={{ padding: '12px 16px', fontSize: '12px', color: 'var(--orange)' }}>
                          {Math.round(gap.scoreGap * 100)} %
                        </td>
                      </tr>
                    )
                  })}
                </tbody>
              </table>
            </div>
          </Card>
        </div>

        {/* Executive Insights (from insightData) */}
        {(() => {
          const execInsights = analysis.insightData || (analysis as any).InsightData || (analysis as any).insight || (analysis as any).insights
          if (!execInsights) return null
          
          return (
          <div className="grid grid-cols-1 lg:grid-cols-2 gap-5 animate-in animate-in-delay-2">
            {/* Why Not Hired */}
            <Card>
              <CardHeader>
                <CardTitle><span style={{ color: 'var(--red)' }}>Why You Won't Get Hired</span></CardTitle>
              </CardHeader>
              <CardBody>
                <p style={{ fontSize: '13px', color: 'var(--white-2)', lineHeight: 1.6 }}>
                  {execInsights.whyNotHired || 'No diagnostic available.'}
                </p>
                {execInsights.summary && (
                  <div className="mt-4 pt-4" style={{ borderTop: '1px solid var(--border)' }}>
                    <p style={{ fontSize: '12px', color: 'var(--white-3)', lineHeight: 1.5 }}>
                      <strong style={{ color: 'var(--white)', fontWeight: 600 }}>Summary: </strong>
                      {execInsights.summary}
                    </p>
                  </div>
                )}
              </CardBody>
            </Card>

            {/* Strengths & Market Context */}
            <Card>
              <CardHeader>
                <CardTitle>Market Context & Strengths</CardTitle>
              </CardHeader>
              <CardBody>
                {execInsights.marketContext && (
                  <p style={{ fontSize: '13px', color: 'var(--white-2)', lineHeight: 1.6, marginBottom: '16px' }}>
                    {execInsights.marketContext}
                  </p>
                )}
                
                <div className="space-y-4">
                  {execInsights.strengths?.length > 0 && (
                    <div>
                      <SectionLabel>Core Strengths</SectionLabel>
                      <div className="flex flex-wrap gap-2">
                        {execInsights.strengths.map((str: string, idx: number) => (
                          <span key={idx} className="rounded-lg font-medium" style={{ fontSize: '11px', padding: '4px 10px', background: 'rgba(34,197,94,0.1)', color: 'var(--green)', border: '1px solid rgba(34,197,94,0.2)' }}>
                            {str}
                          </span>
                        ))}
                      </div>
                    </div>
                  )}
                  {execInsights.criticalGaps?.length > 0 && (
                    <div className="pt-1">
                      <SectionLabel>Critical Feedback</SectionLabel>
                      <div className="flex flex-wrap gap-2">
                        {execInsights.criticalGaps.map((gap: string, idx: number) => (
                          <span key={idx} className="rounded-lg font-medium" style={{ fontSize: '11px', padding: '4px 10px', background: 'rgba(239,68,68,0.1)', color: 'var(--red)', border: '1px solid rgba(239,68,68,0.2)' }}>
                            {gap}
                          </span>
                        ))}
                      </div>
                    </div>
                  )}
                </div>
              </CardBody>
            </Card>
          </div>
          )
        })()}

        {/* Recommendations */}
        <Card className="animate-in animate-in-delay-3">
          <CardHeader>
            <CardTitle>Recommendations</CardTitle>
          </CardHeader>
          <CardBody>
            <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
              {analysis.skillGaps?.filter(g => g.recommendation).map((gap, idx) => (
                <InsightBox key={idx} tag={gap.skillName} text={gap.recommendation as string} />
              ))}
            </div>
            {analysis.skillGaps?.filter(g => g.recommendation).length === 0 && (
              <p style={{ fontSize: '13px', color: 'var(--white-3)' }}>No specific recommendations generated yet.</p>
            )}
          </CardBody>
        </Card>

        {/* Summary breakdown */}
        <div className="grid grid-cols-2 sm:grid-cols-4 gap-5 animate-in animate-in-delay-4">
          <Card>
            <CardHeader><CardTitle>Matched</CardTitle></CardHeader>
            <CardBody style={{ textAlign: 'center', padding: '10px' }}>
              <div className="text-3xl font-syne font-bold" style={{ color: 'var(--green)' }}>{analysis.summary?.matched ?? 0}</div>
            </CardBody>
          </Card>
          <Card>
            <CardHeader><CardTitle>Missing</CardTitle></CardHeader>
            <CardBody style={{ textAlign: 'center', padding: '10px' }}>
              <div className="text-3xl font-syne font-bold" style={{ color: 'var(--red)' }}>{analysis.summary?.missing ?? 0}</div>
            </CardBody>
          </Card>
          <Card>
            <CardHeader><CardTitle>Weak</CardTitle></CardHeader>
            <CardBody style={{ textAlign: 'center', padding: '10px' }}>
              <div className="text-3xl font-syne font-bold" style={{ color: 'var(--amber)' }}>{analysis.summary?.weak ?? 0}</div>
            </CardBody>
          </Card>
          <Card>
            <CardHeader><CardTitle>Unverified</CardTitle></CardHeader>
            <CardBody style={{ textAlign: 'center', padding: '10px' }}>
              <div className="text-3xl font-syne font-bold" style={{ color: 'var(--white-3)' }}>{analysis.summary?.unverified ?? 0}</div>
            </CardBody>
          </Card>
        </div>
      </div>
    </div>
  )
}

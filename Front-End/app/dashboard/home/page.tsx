// 'use client'
// import TopBar from '@/components/layout/TopBar'
// import {
//   StatCard,
//   Card,
//   CardHeader,
//   CardTitle,
//   CardBody,
//   SkillBar,
//   InsightBox,
//   SectionLabel,
//   Button,
// } from '@/components/ui'
// import { mockAnalysis, mockResume } from '@/lib/mock-data'
// import { Plus, CheckCircle2, Circle, Loader2 } from 'lucide-react'
// import Link from 'next/link'

// const STEPS = [
//   { label: 'Upload your resume', sub: 'software_engineer_cv.pdf', status: 'done' },
//   { label: 'Connect GitHub', sub: 'github.com/aryan-k · 34 repos', status: 'done' },
//   { label: 'Select target role', sub: 'Choose the role you\'re applying for', status: 'active' },
//   { label: 'Connect LeetCode', sub: 'Strengthen your profile score', status: 'pending' },
// ] 

// export default function HomePage() {
//   const analysis = mockAnalysis
//   const resume = mockResume

//   return (
//     <div className="animate-in">
//       <TopBar
//         title="Good morning, Aryan"
//         subtitle="Your career diagnosis is ready — 3 critical gaps found"
//         action={
//           <Link href="/dashboard/resume">
//             <Button icon={<Plus size={14} />} size="sm">
//               Upload Resume
//             </Button>
//           </Link>
//         }
//       />

//       <div className="px-4 sm:px-6 lg:px-8 py-6 space-y-5">
//         {/* Stat cards */}
//         <div className="grid grid-cols-2 lg:grid-cols-4 gap-3 animate-in animate-in-delay-1">
//           <StatCard
//             label="Match Score"
//             value="72%"
//             sub="vs Backend Engineer role"
//             subType="muted"
//             valueColor="var(--orange)"
//           />
//           <StatCard
//             label="Skills Verified"
//             value="14"
//             sub="+3 via GitHub"
//             subType="up"
//           />
//           <StatCard
//             label="Critical Gaps"
//             value="3"
//             sub="System Design, K8s, Kafka"
//             subType="down"
//             valueColor="var(--red)"
//           />
//           <StatCard
//             label="Roadmap Progress"
//             value="0%"
//             sub="Coming Soon!"
//             subType="up"
//           />
//         </div>

//         {/* Main 2-col */}
//         <div className="grid grid-cols-1 lg:grid-cols-2 gap-5 animate-in animate-in-delay-2">
//           {/* AI Insights */}
//           <Card>
//             <CardHeader>
//               <CardTitle>Why You Won't Get Hired</CardTitle>
//               <span
//                 className="font-syne font-semibold rounded-xl"
//                 style={{
//                   fontSize: '11px',
//                   color: 'var(--orange)',
//                   background: 'var(--orange-dim)',
//                   padding: '3px 10px',
//                 }}
//               >
//                 AI Insight
//               </span>
//             </CardHeader>
//             <CardBody>
//               {analysis.insights.map((insight) => (
//                 <InsightBox key={insight.tag} tag={insight.tag} text={insight.text} />
//               ))}
//             </CardBody>
//           </Card>

//           {/* Quick Actions */}
//           <Card>
//             <CardHeader>
//               <CardTitle>Setup Checklist</CardTitle>
//               <span
//                 style={{ fontSize: '12px', color: 'var(--white-3)' }}
//               >
//                 3/4 complete
//               </span>
//             </CardHeader>
//             <CardBody style={{ padding: '8px 20px' }}>
//               {STEPS.map((step, i) => (
//                 <div
//                   key={i}
//                   className="flex gap-4 py-3.5"
//                   style={{
//                     borderBottom: i < STEPS.length - 1 ? '1px solid var(--border)' : 'none',
//                   }}
//                 >
//                   <div className="flex-shrink-0 mt-0.5">
//                     {step.status === 'done' ? (
//                       <CheckCircle2 size={18} style={{ color: 'var(--green)' }} />
//                     ) : step.status === 'active' ? (
//                       <Loader2 size={18} style={{ color: 'var(--orange)' }} className="animate-spin" />
//                     ) : (
//                       <Circle size={18} style={{ color: 'var(--white-3)' }} />
//                     )}
//                   </div>
//                   <div className="flex-1 min-w-0">
//                     <p
//                       className="font-medium"
//                       style={{ fontSize: '13px', color: 'var(--white)' }}
//                     >
//                       {step.label}
//                     </p>
//                     <p style={{ fontSize: '11px', color: 'var(--white-3)', marginTop: 2 }}>
//                       {step.sub}
//                     </p>
//                     {step.status === 'active' && (
//                       <p
//                         className="font-semibold mt-1"
//                         style={{ fontSize: '11px', color: 'var(--orange)' }}
//                       >
//                         In progress
//                       </p>
//                     )}
//                     {step.status === 'done' && (
//                       <p
//                         className="font-semibold mt-1"
//                         style={{ fontSize: '11px', color: 'var(--green)' }}
//                       >
//                         Completed
//                       </p>
//                     )}
//                   </div>
//                 </div>
//               ))}
//             </CardBody>
//           </Card>
//         </div>

//         {/* Skill Signal Map */}
//         <Card className="animate-in animate-in-delay-3">
//           <CardHeader>
//             <CardTitle>Skill Signal Map</CardTitle>
//             <span style={{ fontSize: '12px', color: 'var(--white-3)' }}>
//               Confidence per source
//             </span>
//           </CardHeader>
//           <CardBody>
//             <div className="grid grid-cols-1 sm:grid-cols-3 gap-6">
//               <div>
//                 <SectionLabel color="var(--green)">Strengths</SectionLabel>
//                 {analysis.strengths.map((s, i) => (
//                   <SkillBar
//                     key={s.name}
//                     name={s.name}
//                     confidence={s.confidence}
//                     color="var(--green)"
//                     animDelay={i * 80}
//                   />
//                 ))}
//               </div>
//               <div>
//                 <SectionLabel color="var(--amber)">Weak / Unverified</SectionLabel>
//                 {analysis.weakSkills.map((s, i) => (
//                   <SkillBar
//                     key={s.name}
//                     name={s.name}
//                     confidence={s.confidence}
//                     color="var(--amber)"
//                     animDelay={i * 80}
//                   />
//                 ))}
//               </div>
//               <div>
//                 <SectionLabel color="var(--red)">Missing</SectionLabel>
//                 {analysis.missingSkills.map((s, i) => (
//                   <SkillBar
//                     key={s.name}
//                     name={s.name}
//                     confidence={s.confidence || 4}
//                     color="var(--red)"
//                     animDelay={i * 80}
//                   />
//                 ))}
//               </div>
//             </div>
//           </CardBody>
//         </Card>
//       </div>
//     </div>
//   )
// }

import Link from 'next/link'
import { Activity } from 'lucide-react'

export default function HomePage() {
  return (
    <div
      className="min-h-screen flex flex-col items-center justify-center px-4 text-center"
      style={{ background: 'var(--bg)' }}
    >
      {/* Glow */}
      <div
        className="fixed inset-0 pointer-events-none"
        style={{
          background:
            'radial-gradient(ellipse 50% 40% at 50% 0%, rgba(255,92,26,0.07) 0%, transparent 70%)',
        }}
      />

      <div className="relative">
        {/* Logo mark */}
        <div className="flex justify-center mb-8">
          <div
            className="w-12 h-12 rounded-2xl flex items-center justify-center"
            style={{ background: 'var(--orange-dim)', border: '1px solid var(--orange-glow)' }}
          >
            <Activity size={22} style={{ color: 'var(--orange)' }} />
          </div>
        </div>

        {/* 404 */}
        <p
          className="font-syne font-extrabold mb-3"
          style={{ fontSize: '50px', color: 'var(--bg-4)', lineHeight: 1 }}
        >
          Feature Coming soon!
        </p>
        <p
          className="mb-8 max-w-xs mx-auto"
          style={{ fontSize: '14px', color: 'var(--white-3)', lineHeight: 1.7 }}
        >
          Let&apos;s get you back on track.
        </p>

        <Link
          href="/dashboard/analysis"
          className="inline-flex items-center gap-2 px-6 py-2.5 rounded-xl font-syne font-semibold text-white transition-opacity hover:opacity-90"
          style={{ background: 'var(--orange)', fontSize: '14px' }}
        >
          Back to Dashboard
        </Link>
      </div>
    </div>
  )
}
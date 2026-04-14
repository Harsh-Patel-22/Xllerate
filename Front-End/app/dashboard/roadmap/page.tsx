// 'use client'
// import TopBar from '@/components/layout/TopBar'
// import {
//   StatCard, Card, CardHeader, CardTitle, CardBody, Button, SectionLabel,
// } from '@/components/ui'
// import { mockRoadmap } from '@/lib/mock-data'
// import { getRoadmapStatusColor } from '@/lib/utils'
// import { CheckCircle2, Circle, PlayCircle, Download, ArrowRight } from 'lucide-react'

// function StatusIcon({ status }: { status: string }) {
//   if (status === 'complete') return <CheckCircle2 size={17} style={{ color: 'var(--green)' }} />
//   if (status === 'active') return <PlayCircle size={17} style={{ color: 'var(--orange)' }} />
//   return <Circle size={17} style={{ color: 'var(--border-2)' }} />
// }

// export default function RoadmapPage() {
//   const items = mockRoadmap
//   const completed = items.filter((i) => i.status === 'complete').length
//   const totalProgress = Math.round((items.reduce((s, i) => s + i.progress, 0) / items.length))

//   return (
//     <div className="animate-in">
//       <TopBar
//         title="Roadmap"
//         subtitle="Your personalized 12-week plan to close the gaps"
//         action={
//           <div className="flex gap-2">
//             <Button variant="ghost" size="sm" icon={<Download size={13} />}>Export</Button>
//             <Button size="sm" icon={<ArrowRight size={13} />}>Start Week 1</Button>
//           </div>
//         }
//       />

//       <div className="px-4 sm:px-6 lg:px-8 py-6 space-y-5">
//         {/* Stats */}
//         <div className="grid grid-cols-2 lg:grid-cols-4 gap-3 animate-in animate-in-delay-1">
//           <StatCard label="Duration" value="12 wks" sub="At 10 hrs/week" />
//           <StatCard label="Total Tasks" value="18" sub="Across 4 topics" />
//           <StatCard label="Completed" value={`${completed}`} sub={`${totalProgress}% done`} subType="up" valueColor="var(--orange)" />
//           <StatCard label="Est. Score After" value="91%" sub="Strong match" subType="up" valueColor="var(--green)" />
//         </div>

//         <div className="grid grid-cols-1 lg:grid-cols-3 gap-5">
//           {/* Timeline */}
//           <Card className="lg:col-span-2 animate-in animate-in-delay-2">
//             <CardHeader>
//               <CardTitle>Plan</CardTitle>
//               <span style={{ fontSize: '12px', color: 'var(--white-3)' }}>
//                 12 weeks · 4 phases
//               </span>
//             </CardHeader>
//             <CardBody style={{ padding: '4px 20px' }}>
//               {items.map((item, idx) => (
//                 <div
//                   key={item.id}
//                   className="flex gap-4 py-4"
//                   style={{
//                     borderBottom: idx < items.length - 1 ? '1px solid var(--border)' : 'none',
//                   }}
//                 >
//                   {/* Timeline spine */}
//                   <div className="flex flex-col items-center gap-0 w-8 flex-shrink-0">
//                     <div
//                       className="w-2.5 h-2.5 rounded-full mt-1.5 flex-shrink-0"
//                       style={{ background: getRoadmapStatusColor(item.status) }}
//                     />
//                     {idx < items.length - 1 && (
//                       <div
//                         className="w-px flex-1 mt-1"
//                         style={{ background: 'var(--border)', minHeight: '32px' }}
//                       />
//                     )}
//                   </div>

//                   {/* Content */}
//                   <div className="flex-1 min-w-0">
//                     <div className="flex items-start justify-between gap-2 mb-1">
//                       <div>
//                         <p
//                           className="font-semibold uppercase"
//                           style={{
//                             fontSize: '10px',
//                             letterSpacing: '0.08em',
//                             color: 'var(--white-3)',
//                             marginBottom: 3,
//                           }}
//                         >
//                           {item.weeks}
//                         </p>
//                         <p
//                           className="font-syne font-bold"
//                           style={{ fontSize: '14px', color: 'var(--white)' }}
//                         >
//                           {item.title}
//                         </p>
//                       </div>
//                       <StatusIcon status={item.status} />
//                     </div>
//                     <p
//                       style={{ fontSize: '12px', color: 'var(--white-3)', lineHeight: 1.6, marginBottom: 8 }}
//                     >
//                       {item.description}
//                     </p>
//                     <div className="flex flex-wrap gap-2">
//                       {item.pills.map((pill) => (
//                         <span
//                           key={pill}
//                           className="rounded-full"
//                           style={{
//                             fontSize: '11px',
//                             padding: '3px 10px',
//                             background: 'var(--bg-4)',
//                             border: '1px solid var(--border)',
//                             color: 'var(--white-2)',
//                           }}
//                         >
//                           {pill}
//                         </span>
//                       ))}
//                       <span
//                         className="rounded-full font-semibold"
//                         style={{
//                           fontSize: '11px',
//                           padding: '3px 10px',
//                           background:
//                             item.status === 'complete'
//                               ? 'rgba(34,197,94,0.1)'
//                               : item.status === 'active'
//                               ? 'var(--orange-dim)'
//                               : 'var(--bg-4)',
//                           color:
//                             item.status === 'complete'
//                               ? 'var(--green)'
//                               : item.status === 'active'
//                               ? 'var(--orange)'
//                               : 'var(--white-3)',
//                         }}
//                       >
//                         {item.status === 'complete'
//                           ? 'Complete'
//                           : item.status === 'active'
//                           ? 'In Progress'
//                           : 'Upcoming'}
//                       </span>
//                     </div>
//                   </div>
//                 </div>
//               ))}
//             </CardBody>
//           </Card>

//           {/* Right panel */}
//           <div className="space-y-4 animate-in animate-in-delay-3">
//             {/* Progress */}
//             <Card>
//               <CardHeader><CardTitle>Progress</CardTitle></CardHeader>
//               <CardBody>
//                 {items.map((item) => (
//                   <div key={item.id} className="mb-4 last:mb-0">
//                     <div className="flex justify-between mb-1.5">
//                       <span style={{ fontSize: '12px', color: 'var(--white)', fontWeight: 500 }}>
//                         {item.title.split(' ').slice(0, 2).join(' ')}
//                       </span>
//                       <span
//                         style={{
//                           fontSize: '12px',
//                           fontWeight: 600,
//                           color:
//                             item.progress === 100
//                               ? 'var(--green)'
//                               : item.progress > 0
//                               ? 'var(--orange)'
//                               : 'var(--white-3)',
//                         }}
//                       >
//                         {item.progress}%
//                       </span>
//                     </div>
//                     <div
//                       className="h-1.5 rounded-full overflow-hidden"
//                       style={{ background: 'var(--bg-4)' }}
//                     >
//                       <div
//                         className="h-full rounded-full bar-animate"
//                         style={{
//                           width: `${item.progress}%`,
//                           background:
//                             item.progress === 100
//                               ? 'var(--green)'
//                               : 'var(--orange)',
//                         }}
//                       />
//                     </div>
//                   </div>
//                 ))}
//               </CardBody>
//             </Card>

//             {/* Projection */}
//             <Card>
//               <CardHeader><CardTitle>Score Projection</CardTitle></CardHeader>
//               <CardBody>
//                 <div className="flex items-center justify-center gap-6 py-2">
//                   <div className="text-center">
//                     <p style={{ fontSize: '11px', color: 'var(--white-3)', marginBottom: 4 }}>Today</p>
//                     <p
//                       className="font-syne font-extrabold"
//                       style={{ fontSize: '34px', color: 'var(--amber)' }}
//                     >
//                       72%
//                     </p>
//                   </div>
//                   <ArrowRight size={20} style={{ color: 'var(--white-3)', flexShrink: 0 }} />
//                   <div className="text-center">
//                     <p style={{ fontSize: '11px', color: 'var(--white-3)', marginBottom: 4 }}>After Plan</p>
//                     <p
//                       className="font-syne font-extrabold"
//                       style={{ fontSize: '34px', color: 'var(--green)' }}
//                     >
//                       91%
//                     </p>
//                   </div>
//                 </div>
//                 <p
//                   className="text-center mt-3"
//                   style={{ fontSize: '12px', color: 'var(--white-3)', lineHeight: 1.6 }}
//                 >
//                   Complete the roadmap to unlock{' '}
//                   <span style={{ color: 'var(--orange)', fontWeight: 600 }}>Strong Match</span> status
//                 </p>
//               </CardBody>
//             </Card>
//           </div>
//         </div>
//       </div>
//     </div>
//   )
// }

import Link from 'next/link'
import { Activity } from 'lucide-react'

export default function RoadmapPage() {
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


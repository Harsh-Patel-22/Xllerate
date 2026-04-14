// Auth
export interface User {
  id: string
  name: string
  email: string
  avatar?: string
  plan: 'free' | 'pro' | 'enterprise'
  profileCompletion: number
}

// Resume
export type ResumeStatus = 'uploading' | 'processing' | 'parsing' | 'complete' | 'error'

export interface Resume {
  id: string
  filename: string
  uploadedAt: string
  status: ResumeStatus
  skills?: ExtractedSkill[]
  education?: Education[]
  experience?: Experience[]
  links?: ProfileLink[]
}

export interface ExtractedSkill {
  name: string
  confidence: number
  sources: ('resume' | 'github' | 'leetcode')[]
}

export interface Education {
  degree: string
  institution: string
  period: string
  gpa?: string
}

export interface Experience {
  role: string
  company: string
  period: string
  description?: string
}

export interface ProfileLink {
  type: 'github' | 'leetcode' | 'linkedin' | 'portfolio'
  url: string
  connected: boolean
}

// Analysis
export type SkillGapType = 'missing' | 'weak' | 'unverified'
export type SkillSeverity = 'critical' | 'important' | 'nice'

export interface SkillGap {
  skill: string
  gapType: SkillGapType
  severity: SkillSeverity
}

export interface Analysis {
  id: string
  resumeId: string
  targetRole: string
  matchScore: number
  matchLabel: 'strong' | 'competitive' | 'at-risk'
  strengths: ExtractedSkill[]
  weakSkills: ExtractedSkill[]
  missingSkills: ExtractedSkill[]
  gaps: SkillGap[]
  insights: AIInsight[]
  createdAt: string
}

export interface AIInsight {
  tag: string
  text: string
}

// Roadmap
export type RoadmapStatus = 'pending' | 'active' | 'complete'

export interface RoadmapItem {
  id: string
  weeks: string
  title: string
  description: string
  pills: string[]
  status: RoadmapStatus
  progress: number
}

// Jobs
export interface Job {
  id: string
  title: string
  company: string
  companyColor: string
  companyInitial: string
  location: string
  remote: boolean
  salary: string
  experience: string
  skills: string[]
  missingSkills: string[]
  matchScore: number
  postedAt: string
}

// Enrichment
export interface EnrichmentSource {
  type: 'github' | 'leetcode' | 'linkedin'
  connected: boolean
  username?: string
  stats?: Record<string, string | number>
}

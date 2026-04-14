/**
 * Central API client for Xllerate
 * Handles base URL, JWT injection, and error handling.
 * Swap BASE_URL for your backend endpoint.
 */

const BASE_URL = process.env.NEXT_PUBLIC_API_URL ?? 'http://localhost:8000/api'

function getToken(): string | null {
  if (typeof window === 'undefined') return null
  return localStorage.getItem('xllerate_token')
}

async function request<T>(
  path: string,
  options: RequestInit = {}
): Promise<T> {
  const token = getToken()

  const res = await fetch(`${BASE_URL}${path}`, {
    ...options,
    headers: {
      'Content-Type': 'application/json',
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
      ...options.headers,
    },
  })

  if (!res.ok) {
    const body = await res.json().catch(() => ({}))
    throw new Error(body?.detail ?? `API error: ${res.status}`)
  }

  return res.json() as Promise<T>
}

/* ── Auth ──────────────────────────────────────────── */

export interface LoginPayload  { email: string; password: string }
export interface RegisterPayload { name: string; email: string; password: string }
export interface AuthResponse  { access_token: string; user: { id: string; name: string; email: string; plan: string } }

export const authApi = {
  login:    (data: LoginPayload)    => request<AuthResponse>('/auth/login',    { method: 'POST', body: JSON.stringify(data) }),
  register: (data: RegisterPayload) => request<AuthResponse>('/auth/register', { method: 'POST', body: JSON.stringify(data) }),
  me:       ()                       => request<AuthResponse['user']>('/auth/me'),
}

/* ── Resume ────────────────────────────────────────── */

export interface ResumeUploadResponse { resumeId: string }
export interface ResumeStatusResponse { status: 'uploading' | 'processing' | 'parsing' | 'complete' | 'error'; progress: number }

export const resumeApi = {
  upload: (file: File) => {
    const form = new FormData()
    form.append('file', file)
    return request<ResumeUploadResponse>('/resume/upload', {
      method: 'POST',
      body: form,
      headers: {}, // Let browser set Content-Type for multipart
    })
  },
  getStatus: (id: string) =>
    request<ResumeStatusResponse>(`/resume/${id}/status`),
  getById: (id: string) =>
    request(`/resume/${id}`),
  list: () => request<{ resumes: unknown[] }>('/resume'),
  delete: (id: string) =>
    request(`/resume/${id}`, { method: 'DELETE' }),
}

/* ── Analysis ──────────────────────────────────────── */

export interface AnalysisRequest { resumeId: string; roleId: string }

export const analysisApi = {
  run: (data: AnalysisRequest) =>
    request('/analysis', { method: 'POST', body: JSON.stringify(data) }),
  getById: (id: string) => request(`/analysis/${id}`),
  list: () => request('/analysis'),
}

/* ── Jobs ──────────────────────────────────────────── */

export interface JobFilters {
  search?: string
  seniority?: string
  remote?: boolean
  skill?: string
  company?: string
  page?: number
}

export const jobsApi = {
  list: (filters: JobFilters = {}) => {
    const params = new URLSearchParams()
    Object.entries(filters).forEach(([k, v]) => {
      if (v !== undefined && v !== '') params.set(k, String(v))
    })
    return request(`/jobs?${params.toString()}`)
  },
  getById: (id: string) => request(`/jobs/${id}`),
}

/* ── Enrichment ────────────────────────────────────── */

export const enrichmentApi = {
  getStatus: () => request('/enrichment/status'),
  getSkills: () => request('/enrichment/skills'),
  connectGitHub: (username: string) =>
    request('/enrichment/github', { method: 'POST', body: JSON.stringify({ username }) }),
  connectLeetCode: (username: string) =>
    request('/enrichment/leetcode', { method: 'POST', body: JSON.stringify({ username }) }),
}

/* ── Roadmap ───────────────────────────────────────── */

export const roadmapApi = {
  get: (resumeId: string) => request(`/roadmap/${resumeId}`),
  updateItem: (itemId: string, progress: number) =>
    request(`/roadmap/item/${itemId}`, { method: 'PATCH', body: JSON.stringify({ progress }) }),
}

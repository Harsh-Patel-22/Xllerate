'use client'
import { create } from 'zustand'
import type { User } from '@/types'
import { mockUser } from '@/lib/mock-data'

interface UIStore {
  sidebarOpen: boolean
  setSidebarOpen: (open: boolean) => void
  toggleSidebar: () => void
  activeResumeId: string
  setActiveResumeId: (id: string) => void
  selectedRole: string
  setSelectedRole: (role: string) => void
  user: User
}

export const useUIStore = create<UIStore>((set) => ({
  sidebarOpen: false,
  setSidebarOpen: (open) => set({ sidebarOpen: open }),
  toggleSidebar: () => set((s) => ({ sidebarOpen: !s.sidebarOpen })),
  activeResumeId: 'r1',
  setActiveResumeId: (id) => set({ activeResumeId: id }),
  selectedRole: 'Senior Backend Engineer',
  setSelectedRole: (role) => set({ selectedRole: role }),
  user: mockUser,
}))

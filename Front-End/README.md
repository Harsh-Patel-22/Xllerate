# Xllerate — Career Diagnosis Engine

A production-grade Next.js 14 frontend for Xllerate — built with App Router, Tailwind CSS, Zustand, and Framer Motion.

## ✨ Features

- **Dark theme** — Black (#0A0A0A) base with orange (#FF5C1A) accents and off-white text
- **5 dashboard screens** — Overview, Resume, Analysis, Roadmap, Jobs, Profile
- **Auth screens** — Login and Register with form validation
- **Fully responsive** — Mobile-first with hamburger sidebar drawer
- **Animated score ring** — SVG ring with smooth CSS transition
- **Live file upload** — Drag-and-drop with simulated processing pipeline
- **Zustand state** — Lightweight global store for UI and user state
- **Syne + DM Sans** — Distinctive display/body font pairing

## 🚀 Quick Start

```bash
# Install dependencies
npm install

# Start dev server
npm run dev
```

Open [http://localhost:3000](http://localhost:3000) — it redirects to `/dashboard/home`.

## 📁 Folder Structure

```
xllerate/
├── app/
│   ├── auth/
│   │   ├── login/page.tsx
│   │   └── register/page.tsx
│   └── dashboard/
│       ├── home/page.tsx        ← Overview + AI insights
│       ├── resume/page.tsx      ← Upload + extracted profile
│       ├── analysis/page.tsx    ← Score ring + gap table
│       ├── roadmap/page.tsx     ← 12-week plan timeline
│       ├── jobs/page.tsx        ← Matched job listings
│       └── profile/page.tsx     ← User profile + enrichment
├── components/
│   ├── layout/
│   │   ├── Sidebar.tsx          ← Responsive sidebar (desktop fixed, mobile drawer)
│   │   └── TopBar.tsx           ← Sticky header with hamburger
│   ├── resume/
│   │   └── ResumeUploader.tsx   ← Dropzone with upload state machine
│   └── ui/
│       ├── index.tsx            ← Button, Badge, Card, StatCard, SkillBar, InsightBox
│       └── ScoreRing.tsx        ← Animated SVG score ring
├── lib/
│   ├── mock-data.ts             ← All mock data (swap with real API later)
│   └── utils.ts                 ← cn(), color helpers, ring math
├── store/
│   └── uiStore.ts               ← Zustand store
└── types/
    └── index.ts                 ← TypeScript interfaces
```

## 🔌 Connecting to a Real Backend

All data lives in `lib/mock-data.ts`. To wire up real APIs:

1. Create `lib/api.ts` with your base URL and JWT injection
2. Replace `mockAnalysis`, `mockResume`, etc. in each page with `useQuery` calls (TanStack Query)
3. The `ResumeUploader` component's `simulateUpload` can be swapped with a real `POST /api/resume/upload` + polling `GET /api/resume/{id}/status`

## 🎨 Design Tokens

All design tokens are CSS variables in `app/globals.css`:

| Token           | Value        | Usage                  |
|-----------------|--------------|------------------------|
| `--orange`      | `#FF5C1A`    | Primary accent, CTAs   |
| `--bg`          | `#0A0A0A`    | Page background        |
| `--bg-2`        | `#111111`    | Cards, sidebar         |
| `--bg-3`        | `#161616`    | Input backgrounds      |
| `--bg-4`        | `#1C1C1C`    | Hover states, tags     |
| `--white`       | `#F5F2ED`    | Primary text           |
| `--white-2`     | `#C8C4BC`    | Secondary text         |
| `--white-3`     | `#7A7671`    | Muted/placeholder text |
| `--green`       | `#22C55E`    | Success, strengths     |
| `--amber`       | `#F59E0B`    | Warnings, weak skills  |
| `--red`         | `#EF4444`    | Errors, missing skills |

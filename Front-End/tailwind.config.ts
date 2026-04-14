import type { Config } from 'tailwindcss'

const config: Config = {
  content: [
    './pages/**/*.{js,ts,jsx,tsx,mdx}',
    './components/**/*.{js,ts,jsx,tsx,mdx}',
    './app/**/*.{js,ts,jsx,tsx,mdx}',
  ],
  theme: {
    extend: {
      fontFamily: {
        syne: ['var(--font-syne)', 'sans-serif'],
        dm: ['var(--font-dm-sans)', 'sans-serif'],
      },
      colors: {
        bg: {
          DEFAULT: '#0A0A0A',
          2: '#111111',
          3: '#161616',
          4: '#1C1C1C',
          5: '#222222',
        },
        orange: {
          DEFAULT: '#FF5C1A',
          2: '#FF7A3D',
          dim: 'rgba(255,92,26,0.12)',
          glow: 'rgba(255,92,26,0.25)',
        },
        white: {
          DEFAULT: '#F5F2ED',
          2: '#C8C4BC',
          3: '#7A7671',
        },
        border: {
          DEFAULT: 'rgba(255,255,255,0.07)',
          2: 'rgba(255,255,255,0.12)',
          3: 'rgba(255,255,255,0.18)',
        },
      },
      borderRadius: {
        DEFAULT: '8px',
        lg: '12px',
        xl: '16px',
        '2xl': '20px',
      },
      animation: {
        'fade-in': 'fadeIn 0.4s ease forwards',
        'slide-up': 'slideUp 0.4s ease forwards',
        'pulse-orange': 'pulseOrange 2s ease-in-out infinite',
        'spin-slow': 'spin 3s linear infinite',
      },
      keyframes: {
        fadeIn: {
          '0%': { opacity: '0' },
          '100%': { opacity: '1' },
        },
        slideUp: {
          '0%': { opacity: '0', transform: 'translateY(12px)' },
          '100%': { opacity: '1', transform: 'translateY(0)' },
        },
        pulseOrange: {
          '0%, 100%': { boxShadow: '0 0 0 0 rgba(255,92,26,0)' },
          '50%': { boxShadow: '0 0 0 6px rgba(255,92,26,0.15)' },
        },
      },
    },
  },
  plugins: [],
}

export default config

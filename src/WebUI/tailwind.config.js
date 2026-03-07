/** @type {import('tailwindcss').Config} */
export default {
  content: [
    './index.html',
    './src/**/*.{ts,tsx}',
  ],
  darkMode: 'class',
  theme: {
    extend: {
      colors: {
        ve: {
          base:       'rgb(var(--ve-base) / <alpha-value>)',
          surface:    'rgb(var(--ve-surface) / <alpha-value>)',
          elevated:   'rgb(var(--ve-elevated) / <alpha-value>)',
          glass:      'rgb(var(--ve-glass) / <alpha-value>)',
          border:     'rgb(var(--ve-border) / <alpha-value>)',
          accent:     'rgb(var(--ve-accent) / <alpha-value>)',
          'accent-h': 'rgb(var(--ve-accent-h) / <alpha-value>)',
          text:       'rgb(var(--ve-text) / <alpha-value>)',
          secondary:  'rgb(var(--ve-secondary) / <alpha-value>)',
          muted:      'rgb(var(--ve-muted) / <alpha-value>)',
          track:      'rgb(var(--ve-track) / <alpha-value>)',
        },
      },
      fontFamily: {
        ui:   ['Inter', 'system-ui', 'sans-serif'],
        mono: ['Cascadia Mono', 'Consolas', 'monospace'],
      },
      boxShadow: {
        'glow-amber':   '0 0 15px rgba(251, 191, 36, 0.3)',
        'glow-amber-sm': '0 0 8px rgba(251, 191, 36, 0.2)',
        'glow-amber-lg': '0 0 25px rgba(251, 191, 36, 0.35)',
        'glass':        '0 8px 32px rgba(0, 0, 0, 0.3)',
        'glass-sm':     '0 4px 16px rgba(0, 0, 0, 0.2)',
        'glass-lg':     '0 16px 48px rgba(0, 0, 0, 0.4)',
        'inner-highlight': 'inset 0 1px 0 0 rgba(255, 255, 255, 0.05)',
        'inner-highlight-strong': 'inset 0 1px 0 0 rgba(255, 255, 255, 0.1)',
        'depth':        '0 1px 3px rgba(0, 0, 0, 0.3), 0 4px 12px rgba(0, 0, 0, 0.2)',
      },
      borderRadius: {
        '4xl': '2rem',
      },
      animation: {
        'fade-in':     've-fade-in 0.2s ease-out both',
        'scale-in':    've-scale-in 0.2s cubic-bezier(0.34, 1.56, 0.64, 1) both',
        'slide-up':    've-slide-up 0.15s ease-out both',
        'pulse-soft':  've-pulse-soft 1.4s ease-in-out infinite',
      },
      keyframes: {
        've-fade-in': {
          from: { opacity: '0' },
          to:   { opacity: '1' },
        },
        've-scale-in': {
          from: { opacity: '0', transform: 'scale(0.96)' },
          to:   { opacity: '1', transform: 'scale(1)' },
        },
        've-slide-up': {
          from: { opacity: '0', transform: 'translateY(3px)' },
          to:   { opacity: '1', transform: 'translateY(0)' },
        },
        've-pulse-soft': {
          '0%, 100%': { opacity: '1' },
          '50%':      { opacity: '0.7' },
        },
      },
    },
  },
  plugins: [],
}

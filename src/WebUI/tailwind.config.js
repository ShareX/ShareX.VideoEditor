/** @type {import('tailwindcss').Config} */
export default {
  content: [
    './index.html',
    './src/**/*.{ts,tsx}',
  ],
  theme: {
    extend: {
      colors: {
        // CSS variables — declared in index.css, toggled by data-theme attribute.
        // Using rgb() + <alpha-value> so Tailwind opacity modifiers (e.g. bg-ve-base/80) work.
        ve: {
          base:       'rgb(var(--ve-base) / <alpha-value>)',
          surface:    'rgb(var(--ve-surface) / <alpha-value>)',
          elevated:   'rgb(var(--ve-elevated) / <alpha-value>)',
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
    },
  },
  plugins: [],
}

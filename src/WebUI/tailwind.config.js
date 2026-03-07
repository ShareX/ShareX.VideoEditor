/** @type {import('tailwindcss').Config} */
export default {
  content: [
    './index.html',
    './src/**/*.{ts,tsx}',
  ],
  theme: {
    extend: {
      colors: {
        ve: {
          base:      '#08080f',
          surface:   '#111118',
          elevated:  '#1a1a24',
          border:    '#2a2a3a',
          accent:    '#7c6dfa',
          'accent-h':'#9d91fc',
          text:      '#f0f0f8',
          secondary: '#9090a8',
          muted:     '#555568',
          track:     '#0d0d14',
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

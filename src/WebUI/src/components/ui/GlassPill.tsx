import type { ReactNode } from 'react'

interface GlassPillProps {
  children: ReactNode
  className?: string
}

export function GlassPill({ children, className = '' }: GlassPillProps) {
  return (
    <div
      className={`
        ve-glass rounded-full
        ring-1 ring-white/[0.06]
        shadow-inner-highlight shadow-glass-sm
        flex items-center
        ${className}
      `}
    >
      {children}
    </div>
  )
}

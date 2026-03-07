import type { ReactNode } from 'react'

interface SectionLabelProps {
  children: ReactNode
}

export function SectionLabel({ children }: SectionLabelProps) {
  return (
    <p className="text-[10px] font-semibold tracking-[0.15em] text-ve-muted uppercase mb-2">
      {children}
    </p>
  )
}

import type { ReactNode } from 'react'

interface FieldRowProps {
  label: string
  children: ReactNode
}

export function FieldRow({ label, children }: FieldRowProps) {
  return (
    <div className="flex items-center justify-between gap-3">
      <span className="text-xs text-ve-secondary shrink-0">{label}</span>
      <span className="text-xs font-mono text-ve-text tracking-tighter">{children}</span>
    </div>
  )
}

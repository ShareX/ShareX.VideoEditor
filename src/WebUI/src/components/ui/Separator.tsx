interface SeparatorProps {
  orientation?: 'horizontal' | 'vertical'
  className?: string
}

export function Separator({ orientation = 'vertical', className = '' }: SeparatorProps) {
  return orientation === 'vertical'
    ? <div className={`w-px h-5 bg-white/[0.08] shrink-0 ${className}`} />
    : <div className={`h-px w-full bg-white/[0.08] shrink-0 ${className}`} />
}

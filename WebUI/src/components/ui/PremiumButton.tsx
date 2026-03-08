import type { ReactNode, ButtonHTMLAttributes } from 'react'

interface PremiumButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  children: ReactNode
  variant?: 'primary' | 'secondary' | 'ghost' | 'destructive'
  size?: 'sm' | 'md' | 'lg'
  icon?: ReactNode
}

export function PremiumButton({
  children,
  variant = 'primary',
  size = 'md',
  icon,
  className = '',
  disabled,
  ...props
}: PremiumButtonProps) {
  const base = `
    inline-flex items-center justify-center gap-2
    font-medium rounded-xl
    transition-all duration-200 ease-out
    focus-visible:ring-2 focus-visible:ring-amber-400/50 focus-visible:outline-hidden
    active:scale-[0.97]
    disabled:opacity-40 disabled:pointer-events-none
  `

  const sizeMap = {
    sm: 'h-8 px-3 text-xs',
    md: 'h-10 px-5 text-sm',
    lg: 'h-12 px-6 text-sm',
  } as const

  const variants = {
    primary: `
      bg-ve-accent text-white font-semibold
      shadow-glow-amber-sm
      hover:bg-ve-accent-h hover:shadow-glow-amber
    `,
    secondary: `
      bg-ve-elevated text-ve-text
      ring-1 ring-white/8
      shadow-inner-highlight
      hover:bg-ve-border/60
    `,
    ghost: `
      text-ve-secondary
      hover:text-ve-text hover:bg-white/[0.07]
    `,
    destructive: `
      bg-red-500/10 text-red-400
      ring-1 ring-red-500/20
      hover:bg-red-500/20
    `,
  } as const

  return (
    <button
      className={`${base} ${sizeMap[size]} ${variants[variant]} ${className}`}
      disabled={disabled}
      {...props}
    >
      {icon && <span className="shrink-0">{icon}</span>}
      {children}
    </button>
  )
}

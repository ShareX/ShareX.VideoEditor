import type { ReactNode, ButtonHTMLAttributes } from 'react'

interface PremiumIconButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  children: ReactNode
  active?: boolean
  size?: 'sm' | 'md' | 'lg'
  variant?: 'ghost' | 'surface' | 'accent'
}

const sizeMap = {
  sm: 'w-8 h-8',
  md: 'w-10 h-10',
  lg: 'w-12 h-12',
} as const

export function PremiumIconButton({
  children,
  active = false,
  size = 'md',
  variant = 'ghost',
  className = '',
  disabled,
  ...props
}: PremiumIconButtonProps) {
  const base = `
    ${sizeMap[size]}
    rounded-xl
    flex items-center justify-center
    transition-all duration-200 ease-out
    focus-visible:ring-2 focus-visible:ring-amber-400/50 focus-visible:outline-hidden
  `

  const variants = {
    ghost: `
      text-ve-secondary
      hover:text-ve-text hover:bg-white/[0.07]
      active:scale-[0.95]
    `,
    surface: `
      bg-ve-elevated/60 text-ve-secondary
      ring-1 ring-white/6
      hover:text-ve-text hover:bg-ve-elevated
      active:scale-[0.95]
    `,
    accent: `
      bg-ve-accent text-white
      shadow-glow-amber-sm
      hover:bg-ve-accent-h hover:shadow-glow-amber
      active:scale-[0.95]
    `,
  } as const

  const activeStyles = active
    ? 'ring-1 ring-amber-400 shadow-glow-amber bg-amber-400/10 text-amber-400'
    : ''

  const disabledStyles = disabled
    ? 'opacity-40 pointer-events-none'
    : ''

  return (
    <button
      className={`${base} ${variants[variant]} ${activeStyles} ${disabledStyles} ${className}`}
      disabled={disabled}
      {...props}
    >
      {children}
    </button>
  )
}

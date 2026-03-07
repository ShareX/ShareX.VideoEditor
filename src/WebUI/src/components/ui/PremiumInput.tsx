import type { InputHTMLAttributes } from 'react'

interface PremiumInputProps extends InputHTMLAttributes<HTMLInputElement> {
  label?: string
}

export function PremiumInput({ label, className = '', ...props }: PremiumInputProps) {
  return (
    <div className="space-y-1.5">
      {label && (
        <label className="text-[11px] font-medium text-ve-secondary tracking-wide">
          {label}
        </label>
      )}
      <input
        className={`
          w-full h-9 px-3 text-xs text-ve-text
          bg-ve-elevated/60 rounded-xl
          ring-1 ring-white/[0.06]
          shadow-inner-highlight
          placeholder:text-ve-muted
          focus:outline-none focus:ring-1 focus:ring-amber-400/50
          transition-all duration-200
          ${className}
        `}
        {...props}
      />
    </div>
  )
}

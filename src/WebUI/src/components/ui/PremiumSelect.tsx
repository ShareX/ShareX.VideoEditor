import type { SelectHTMLAttributes } from 'react'

interface PremiumSelectProps extends SelectHTMLAttributes<HTMLSelectElement> {
  label?: string
  options: { value: string; label: string }[]
}

export function PremiumSelect({ label, options, className = '', ...props }: PremiumSelectProps) {
  return (
    <div className="space-y-1.5">
      {label && (
        <label className="text-[11px] font-medium text-ve-secondary tracking-wide">
          {label}
        </label>
      )}
      <select
        className={`
          w-full h-9 px-3 text-xs text-ve-text
          bg-ve-elevated/60 rounded-xl
          ring-1 ring-white/[0.06]
          shadow-inner-highlight
          focus:outline-none focus:ring-1 focus:ring-amber-400/50
          transition-all duration-200
          appearance-none cursor-pointer
          ${className}
        `}
        {...props}
      >
        {options.map(o => (
          <option key={o.value} value={o.value}>{o.label}</option>
        ))}
      </select>
    </div>
  )
}

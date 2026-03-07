import type { InputHTMLAttributes } from 'react'

interface PremiumSliderProps extends Omit<InputHTMLAttributes<HTMLInputElement>, 'type'> {
  label?: string
  displayValue?: string
}

export function PremiumSlider({ label, displayValue, className = '', ...props }: PremiumSliderProps) {
  return (
    <div className="space-y-1.5">
      {(label || displayValue) && (
        <div className="flex items-center justify-between">
          {label && (
            <span className="text-[11px] font-medium text-ve-secondary tracking-wide">{label}</span>
          )}
          {displayValue && (
            <span className="text-[11px] font-mono text-ve-text tracking-tighter">{displayValue}</span>
          )}
        </div>
      )}
      <input
        type="range"
        className={`w-full ${className}`}
        {...props}
      />
    </div>
  )
}

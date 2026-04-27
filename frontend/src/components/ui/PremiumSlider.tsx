import { useId } from 'react'
import type { InputHTMLAttributes } from 'react'

interface PremiumSliderProps extends Omit<InputHTMLAttributes<HTMLInputElement>, 'type'> {
  label?: string
  displayValue?: string
}

export function PremiumSlider({ label, displayValue, className = '', id, ...props }: PremiumSliderProps) {
  const generatedId = useId()
  const sliderId = id ?? generatedId

  return (
    <div className="space-y-1.5">
      {(label || displayValue) && (
        <div className="flex items-center justify-between">
          {label && (
            <label htmlFor={sliderId} className="text-[11px] font-medium text-ve-secondary tracking-wide">{label}</label>
          )}
          {displayValue && (
            <span className="text-[11px] font-mono text-ve-text tracking-tighter">{displayValue}</span>
          )}
        </div>
      )}
      <input
        id={sliderId}
        type="range"
        className={`w-full ${className}`}
        {...props}
      />
    </div>
  )
}

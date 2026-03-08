interface PremiumToggleProps {
  checked: boolean
  onChange: (checked: boolean) => void
  label?: string
  disabled?: boolean
}

export function PremiumToggle({ checked, onChange, label, disabled = false }: PremiumToggleProps) {
  return (
    <label className={`inline-flex items-center gap-2.5 cursor-pointer select-none ${disabled ? 'opacity-40 pointer-events-none' : ''}`}>
      <button
        role="switch"
        type="button"
        aria-checked={checked}
        onClick={() => onChange(!checked)}
        disabled={disabled}
        className={`
          relative w-9 h-5 rounded-full
          transition-all duration-200 ease-out
          focus-visible:ring-2 focus-visible:ring-amber-400/50 focus-visible:outline-hidden
          ${checked
            ? 'bg-ve-accent shadow-glow-amber-sm'
            : 'bg-ve-border'
          }
        `}
      >
        <span
          className={`
            absolute top-0.5 left-0.5
            w-4 h-4 rounded-full bg-white
            shadow-depth
            transition-transform duration-200 ease-out
            ${checked ? 'translate-x-4' : 'translate-x-0'}
          `}
        />
      </button>
      {label && (
        <span className="text-xs text-ve-text">{label}</span>
      )}
    </label>
  )
}

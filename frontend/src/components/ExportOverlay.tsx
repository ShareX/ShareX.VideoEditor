import { X, Loader2 } from 'lucide-react'
import { PremiumButton, PremiumIconButton } from './ui'

interface ExportOverlayProps {
  progress: number
  message: string
  onCancel: () => void
}

export default function ExportOverlay({ progress, message, onCancel }: ExportOverlayProps) {
  return (
    <div className="absolute inset-0 bg-ve-base/80 backdrop-blur-md flex items-center justify-center z-50">
      <div
        className="
          animate-scale-in
          ve-glass-heavy rounded-3xl
          ring-1 ring-white/8
          shadow-glass-lg
          p-8 w-[340px]
          flex flex-col items-center gap-6
        "
        role="dialog"
        aria-modal="true"
        aria-label="Export progress"
      >
        {/* Animated icon */}
        <div className="w-14 h-14 rounded-2xl bg-ve-accent/15 ring-1 ring-amber-400/30 shadow-glow-amber flex items-center justify-center">
          <Loader2 className="w-6 h-6 text-amber-400 animate-spin" />
        </div>

        <div className="flex flex-col items-center gap-1">
          <p className="text-lg font-semibold text-ve-text tracking-tight">Exporting…</p>
          <p className="text-xs text-ve-secondary">{message}</p>
        </div>

        {/* Progress bar */}
        <div className="w-full space-y-2">
          <div
            className="w-full h-2 bg-ve-elevated rounded-full overflow-hidden ring-1 ring-white/6"
            role="progressbar"
            aria-valuenow={progress}
            aria-valuemin={0}
            aria-valuemax={100}
          >
            <div
              className="h-full bg-linear-to-r from-amber-500 to-amber-400 rounded-full transition-all duration-300 shadow-glow-amber-sm animate-pulse-soft"
              style={{ width: `${progress}%` }}
            />
          </div>
          <div className="flex justify-between">
            <span className="text-[11px] font-mono text-ve-muted tracking-tighter">{Math.round(progress)}%</span>
            <span className="text-[11px] text-ve-muted">Please wait…</span>
          </div>
        </div>

        {/* Cancel */}
        <div className="flex items-center gap-2">
          <PremiumButton
            onClick={onCancel}
            variant="ghost"
            size="sm"
            icon={<X className="w-3.5 h-3.5" />}
          >
            Cancel
          </PremiumButton>
        </div>

        {/* Close corner button */}
        <PremiumIconButton
          onClick={onCancel}
          size="sm"
          variant="ghost"
          className="absolute top-4 right-4"
          aria-label="Cancel export"
        >
          <X className="w-4 h-4" />
        </PremiumIconButton>
      </div>
    </div>
  )
}

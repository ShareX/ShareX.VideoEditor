interface ExportOverlayProps {
  progress: number
  message: string
  onCancel: () => void
}

export default function ExportOverlay({ progress, message, onCancel }: ExportOverlayProps) {
  return (
    <div className="absolute inset-0 bg-ve-base/80 backdrop-blur-sm flex items-center justify-center z-50">
      <div
        className="ve-overlay-card bg-ve-surface border border-ve-border rounded-2xl p-8 w-80 flex flex-col items-center gap-5 shadow-2xl"
        role="dialog"
        aria-modal="true"
        aria-label="Export progress"
      >
        <p className="text-lg font-bold text-ve-text tracking-tight">Exporting…</p>

        {/* Progress bar */}
        <div className="w-full h-1.5 bg-ve-border rounded-full overflow-hidden" role="progressbar" aria-valuenow={progress} aria-valuemin={0} aria-valuemax={100}>
          <div
            className="h-full bg-ve-accent ve-progress-active rounded-full transition-all duration-300"
            style={{ width: `${progress}%` }}
          />
        </div>

        <p className="text-sm text-ve-secondary text-center" aria-live="polite">{message}</p>

        <button
          onClick={onCancel}
          className="px-5 py-1.5 text-xs font-medium rounded-md border border-ve-border
                     text-ve-secondary hover:text-ve-text hover:border-ve-secondary transition-colors"
        >
          Cancel
        </button>
      </div>
    </div>
  )
}

interface HeaderProps {
  videoPath: string
  isExporting: boolean
  exportProgress: number
  exportStatusMessage: string
  ffmpegAvailable: boolean
  onExport: () => void
}

export default function Header({
  videoPath,
  isExporting,
  exportProgress,
  exportStatusMessage,
  ffmpegAvailable,
  onExport,
}: HeaderProps) {
  const fileName = videoPath ? videoPath.split('/').pop()?.split('\\').pop() ?? videoPath : ''

  return (
    <header className="flex items-center gap-3 h-12 px-4 bg-ve-surface border-b border-ve-border shrink-0">
      {/* Logo + title */}
      <div className="flex items-center gap-2.5">
        <div className="w-7 h-7 rounded-md bg-ve-accent flex items-center justify-center text-xs text-ve-base font-bold">
          ▶
        </div>
        <span className="text-xs font-semibold tracking-widest text-ve-text uppercase">
          Video Editor
        </span>
        <span className="w-px h-4 bg-ve-border" />
        <span className="font-mono text-[11px] text-ve-secondary truncate max-w-xs">
          {fileName}
        </span>
      </div>

      {/* Center: export progress */}
      <div className="flex items-center gap-2 flex-1 justify-center" aria-live="polite" aria-atomic="true">
        {isExporting && (
          <>
            <span className="text-xs text-ve-accent">{exportStatusMessage}</span>
            <div className="w-28 h-1 bg-ve-border rounded-full overflow-hidden" role="progressbar" aria-valuenow={exportProgress} aria-valuemin={0} aria-valuemax={100}>
              <div
                className="h-full bg-ve-accent ve-progress-active rounded-full transition-all duration-300"
                style={{ width: `${exportProgress}%` }}
              />
            </div>
          </>
        )}
      </div>

      <div className="ml-auto flex items-center gap-2">
        {!ffmpegAvailable && (
          <span className="text-[11px] text-amber-400 bg-amber-400/10 px-2 py-0.5 rounded">
            FFmpeg not found
          </span>
        )}
        <button
          onClick={onExport}
          disabled={isExporting || !ffmpegAvailable}
          className="px-4 py-1.5 text-xs font-semibold rounded-md bg-ve-accent text-white
                     hover:bg-ve-accent-h active:scale-95 transition-all
                     disabled:opacity-40 disabled:cursor-not-allowed"
          title="Export video (Ctrl+E)"
          aria-label="Export video"
          aria-keyshortcuts="Control+E"
        >
          Export
        </button>
      </div>
    </header>
  )
}

import type { RefObject } from 'react'

interface VideoPlayerProps {
  videoRef: RefObject<HTMLVideoElement>
  videoUrl: string
  isExporting: boolean
  exportProgress: number
  exportStatusMessage: string
  isCropMode: boolean
  onDurationChange: () => void
  onTimeUpdate: () => void
  onCancelExport: () => void
}

export default function VideoPlayer({
  videoRef,
  videoUrl,
  isExporting,
  isCropMode,
  onDurationChange,
  onTimeUpdate,
}: VideoPlayerProps) {
  return (
    <div className="relative flex-1 min-h-0 bg-black flex items-center justify-center overflow-hidden">
      {videoUrl ? (
        <video
          ref={videoRef}
          src={videoUrl}
          className="max-w-full max-h-full object-contain"
          onDurationChange={onDurationChange}
          onTimeUpdate={onTimeUpdate}
          onEnded={() => {}}
          // Suppress browser context menu
          onContextMenu={e => e.preventDefault()}
        />
      ) : (
        /* Placeholder before config arrives */
        <div className="flex flex-col items-center gap-4 text-ve-secondary">
          <div className="w-16 h-16 rounded-full bg-ve-elevated flex items-center justify-center text-2xl">
            ▶
          </div>
          <span className="text-sm">Loading video…</span>
        </div>
      )}

      {/* Crop mode overlay */}
      {isCropMode && (
        <div className="absolute inset-0 bg-black/50 pointer-events-none">
          <div className="absolute inset-[10%] border-2 border-ve-accent bg-white/5" />
        </div>
      )}

      {/* Exporting dim */}
      {isExporting && (
        <div className="absolute inset-0 bg-ve-base/70 pointer-events-none" />
      )}
    </div>
  )
}

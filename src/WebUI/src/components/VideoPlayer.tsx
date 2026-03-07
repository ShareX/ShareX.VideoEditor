import type { RefObject } from 'react'
import { Film, Loader2 } from 'lucide-react'

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
    <div className="relative flex-1 min-h-0 bg-ve-base flex items-center justify-center overflow-hidden">
      {videoUrl ? (
        <video
          ref={videoRef}
          src={videoUrl}
          className="max-w-full max-h-full object-contain"
          onDurationChange={onDurationChange}
          onTimeUpdate={onTimeUpdate}
          onEnded={() => {}}
          onContextMenu={e => e.preventDefault()}
        />
      ) : (
        <div className="flex flex-col items-center gap-5 text-ve-secondary">
          <div className="w-20 h-20 rounded-3xl ve-glass ring-1 ring-white/[0.08] shadow-glass flex items-center justify-center">
            <Film className="w-8 h-8 text-ve-muted" />
          </div>
          <div className="flex flex-col items-center gap-1.5">
            <span className="text-sm font-medium text-ve-secondary">Loading video…</span>
            <Loader2 className="w-4 h-4 text-ve-muted animate-spin" />
          </div>
        </div>
      )}

      {/* Crop mode overlay */}
      {isCropMode && (
        <div className="absolute inset-0 bg-black/60 pointer-events-none">
          <div className="absolute inset-[10%] border-2 border-amber-400/70 bg-white/5 rounded-lg shadow-glow-amber" />
        </div>
      )}

      {/* Exporting dim */}
      {isExporting && (
        <div className="absolute inset-0 bg-ve-base/75 backdrop-blur-sm pointer-events-none" />
      )}
    </div>
  )
}

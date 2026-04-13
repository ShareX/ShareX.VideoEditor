import type { RefObject } from 'react'
import { AlertTriangle, ExternalLink, Film, Loader2 } from 'lucide-react'

interface VideoPlayerProps {
  videoRef: RefObject<HTMLVideoElement | null>
  videoUrl: string
  videoCodec: string
  isExporting: boolean
  exportProgress: number
  exportStatusMessage: string
  isCropMode: boolean
  playbackIssueMessage: string
  playbackHelpUrl: string
  onDurationChange: () => void
  onTimeUpdate: () => void
  onPlaybackStateChange: () => void
  onPlaybackReady: () => void
  onPlaybackError: (errorCode?: number) => void
  onCancelExport: () => void
}

export default function VideoPlayer({
  videoRef,
  videoUrl,
  videoCodec,
  isExporting,
  isCropMode,
  playbackIssueMessage,
  playbackHelpUrl,
  onDurationChange,
  onTimeUpdate,
  onPlaybackStateChange,
  onPlaybackReady,
  onPlaybackError,
}: VideoPlayerProps) {
  return (
    <div className="relative flex-1 min-h-0 bg-ve-base flex items-center justify-center overflow-hidden">
      {videoUrl ? (
        <video
          ref={videoRef}
          src={videoUrl}
          preload="auto"
          className="max-w-full max-h-full object-contain"
          onDurationChange={onDurationChange}
          onTimeUpdate={onTimeUpdate}
          onLoadedData={onPlaybackReady}
          onCanPlay={onPlaybackReady}
          onPlay={onPlaybackStateChange}
          onPause={onPlaybackStateChange}
          onEnded={onPlaybackStateChange}
          onError={e => {
            const err = (e.target as HTMLVideoElement).error
            console.error('Video load error:', err?.code, err?.message)
            onPlaybackError(err?.code)
          }}
          onContextMenu={e => e.preventDefault()}
        />
      ) : (
        <div className="flex flex-col items-center gap-5 text-ve-secondary">
          <div className="w-20 h-20 rounded-3xl ve-glass ring-1 ring-white/8 shadow-glass flex items-center justify-center">
            <Film className="w-8 h-8 text-ve-muted" />
          </div>
          <div className="flex flex-col items-center gap-1.5">
            <span className="text-sm font-medium text-ve-secondary">Loading video…</span>
            <Loader2 className="w-4 h-4 text-ve-muted animate-spin" />
          </div>
        </div>
      )}

      {playbackIssueMessage && (
        <div className="absolute inset-0 flex items-center justify-center p-6">
          <div className="max-w-xl rounded-[28px] border border-amber-400/20 bg-ve-surface/92 backdrop-blur-md shadow-glass px-6 py-5">
            <div className="flex items-start gap-4">
              <div className="w-11 h-11 shrink-0 rounded-2xl bg-amber-400/12 text-amber-300 flex items-center justify-center">
                <AlertTriangle className="w-5 h-5" />
              </div>
              <div className="flex flex-col gap-2">
                <div className="text-sm font-semibold text-ve-text">
                  Embedded playback needs additional support
                </div>
                <div className="text-sm leading-6 text-ve-secondary">
                  {playbackIssueMessage}
                </div>
                {videoCodec && (
                  <div className="text-[11px] font-mono text-ve-muted uppercase tracking-[0.18em]">
                    Codec: {videoCodec}
                  </div>
                )}
                {playbackHelpUrl && (
                  <a
                    href={playbackHelpUrl}
                    target="_blank"
                    rel="noreferrer"
                    className="inline-flex items-center gap-2 text-sm font-medium text-amber-300 hover:text-amber-200"
                  >
                    Open codec guidance
                    <ExternalLink className="w-3.5 h-3.5" />
                  </a>
                )}
              </div>
            </div>
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
        <div className="absolute inset-0 bg-ve-base/75 backdrop-blur-xs pointer-events-none" />
      )}
    </div>
  )
}

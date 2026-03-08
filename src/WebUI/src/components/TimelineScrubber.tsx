import { useCallback, useRef } from 'react'
import { formatTime } from '../utils/time'
import { PremiumButton } from './ui'

interface TimelineScrubberProps {
  duration: number
  position: number
  trimStart: number
  trimEnd: number
  isTrimActive: boolean
  thumbnails: string[]
  onSeek: (seconds: number) => void
  onTrimStartChange: (seconds: number) => void
  onTrimEndChange: (seconds: number) => void
  onSetTrimStart: () => void
  onSetTrimEnd: () => void
  onResetTrim: () => void
}

type DragTarget = 'playhead' | 'trimStart' | 'trimEnd'

export default function TimelineScrubber({
  duration,
  position,
  trimStart,
  trimEnd,
  isTrimActive,
  thumbnails,
  onSeek,
  onTrimStartChange,
  onTrimEndChange,
  onSetTrimStart,
  onSetTrimEnd,
  onResetTrim,
}: TimelineScrubberProps) {
  const trackRef = useRef<HTMLDivElement>(null)
  const dragRef = useRef<DragTarget | null>(null)

  const xToSeconds = useCallback((clientX: number): number => {
    const rect = trackRef.current?.getBoundingClientRect()
    if (!rect || duration === 0) return 0
    const frac = Math.max(0, Math.min(1, (clientX - rect.left) / rect.width))
    return frac * duration
  }, [duration])

  const handlePointerDown = useCallback((e: React.PointerEvent) => {
    if (duration === 0) return
    const rect = trackRef.current?.getBoundingClientRect()
    if (!rect) return
    const x = e.clientX - rect.left
    const w = rect.width

    const startX = (trimStart / duration) * w
    const endX = (trimEnd / duration) * w
    const HIT = 10

    let target: DragTarget
    if (Math.abs(x - startX) <= HIT) target = 'trimStart'
    else if (Math.abs(x - endX) <= HIT) target = 'trimEnd'
    else target = 'playhead'

    dragRef.current = target
    ;(e.target as Element).setPointerCapture(e.pointerId)
    applyDrag(target, e.clientX)
    e.preventDefault()
  }, [duration, trimStart, trimEnd]) // eslint-disable-line

  const applyDrag = useCallback((target: DragTarget, clientX: number) => {
    const t = xToSeconds(clientX)
    if (target === 'playhead') onSeek(t)
    else if (target === 'trimStart') onTrimStartChange(Math.min(t, trimEnd - 0.1))
    else if (target === 'trimEnd') onTrimEndChange(Math.max(t, trimStart + 0.1))
  }, [xToSeconds, onSeek, onTrimStartChange, onTrimEndChange, trimStart, trimEnd])

  const handlePointerMove = useCallback((e: React.PointerEvent) => {
    if (!dragRef.current) return
    applyDrag(dragRef.current, e.clientX)
  }, [applyDrag])

  const handlePointerUp = useCallback((e: React.PointerEvent) => {
    if (dragRef.current) applyDrag(dragRef.current, e.clientX)
    dragRef.current = null
  }, [applyDrag])

  const frac = (s: number) => duration > 0 ? `${(s / duration) * 100}%` : '0%'

  const effectiveTrimStart = isTrimActive ? trimStart : 0
  const effectiveTrimEnd = isTrimActive ? trimEnd : duration

  return (
    <div className="bg-ve-track/80 backdrop-blur-xs px-5 pt-3 pb-2.5 shrink-0 select-none border-t border-white/4">
      {/* Track container */}
      <div
        ref={trackRef}
        className="relative h-14 rounded-2xl overflow-hidden cursor-pointer bg-ve-base ring-1 ring-white/6 shadow-inner-highlight"
        onPointerDown={handlePointerDown}
        onPointerMove={handlePointerMove}
        onPointerUp={handlePointerUp}
      >
        {/* Thumbnails */}
        {thumbnails.length > 0 && (
          <div className="absolute inset-0 flex">
            {thumbnails.map((src, i) => (
              <img
                key={i}
                src={src}
                className="h-full object-cover flex-1"
                draggable={false}
                alt=""
              />
            ))}
          </div>
        )}

        {/* Trim region highlight */}
        {isTrimActive && (
          <div
            className="absolute top-0 bottom-0 bg-amber-400/15 border-x-0"
            style={{ left: frac(effectiveTrimStart), width: `calc(${frac(effectiveTrimEnd)} - ${frac(effectiveTrimStart)})` }}
          />
        )}

        {/* Trim start handle */}
        {isTrimActive && (
          <div
            className="absolute top-0 bottom-0 w-1.5 bg-amber-400 rounded-full cursor-ew-resize shadow-glow-amber-sm"
            style={{ left: frac(effectiveTrimStart), transform: 'translateX(-50%)' }}
          />
        )}

        {/* Trim end handle */}
        {isTrimActive && (
          <div
            className="absolute top-0 bottom-0 w-1.5 bg-amber-400 rounded-full cursor-ew-resize shadow-glow-amber-sm"
            style={{ left: frac(effectiveTrimEnd), transform: 'translateX(-50%)' }}
          />
        )}

        {/* Playhead */}
        <div
          className="absolute top-0 bottom-0 w-0.5 bg-white shadow-[0_0_6px_rgba(255,255,255,0.4)]"
          style={{ left: frac(position), transform: 'translateX(-50%)' }}
        >
          <div className="absolute -top-0.5 left-1/2 -translate-x-1/2 w-3 h-3 bg-white rounded-xs rotate-45 shadow-depth" />
        </div>
      </div>

      {/* Footer row */}
      <div className="flex items-center justify-between mt-2.5">
        <div className="flex items-center gap-4">
          {isTrimActive && (
            <>
              <span className="text-[11px] text-ve-muted font-mono tracking-tighter">
                IN <span className="text-amber-400">{formatTime(effectiveTrimStart)}</span>
              </span>
              <span className="text-[11px] text-ve-muted font-mono tracking-tighter">
                OUT <span className="text-amber-400">{formatTime(effectiveTrimEnd)}</span>
              </span>
              <span className="text-[11px] text-ve-muted font-mono tracking-tighter">
                DUR <span className="text-ve-secondary">{formatTime(effectiveTrimEnd - effectiveTrimStart)}</span>
              </span>
            </>
          )}
        </div>
        <div className="flex items-center gap-1.5">
          <PremiumButton
            onClick={onSetTrimStart}
            variant="ghost"
            size="sm"
            className="h-7! px-2.5! text-[11px]! font-mono"
            title="Set trim In point (I)"
          >
            [ Set In
          </PremiumButton>
          <PremiumButton
            onClick={onSetTrimEnd}
            variant="ghost"
            size="sm"
            className="h-7! px-2.5! text-[11px]! font-mono"
            title="Set trim Out point (O)"
          >
            Set Out ]
          </PremiumButton>
          {isTrimActive && (
            <PremiumButton
              onClick={onResetTrim}
              variant="ghost"
              size="sm"
              className="h-7! px-2.5! text-[11px]! text-ve-muted"
              title="Reset trim"
            >
              Reset
            </PremiumButton>
          )}
        </div>
      </div>
    </div>
  )
}

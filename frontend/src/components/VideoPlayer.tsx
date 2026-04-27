import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import type { PointerEvent as ReactPointerEvent, RefObject } from 'react'
import { Film, Loader2 } from 'lucide-react'

interface VideoPlayerProps {
  videoRef: RefObject<HTMLVideoElement | null>
  videoUrl: string
  isExporting: boolean
  exportProgress: number
  exportStatusMessage: string
  isCropMode: boolean
  isCropActive: boolean
  cropX: number
  cropY: number
  cropWidth: number
  cropHeight: number
  onCropChange: (crop: { cropX: number; cropY: number; cropWidth: number; cropHeight: number }) => void
  onDurationChange: () => void
  onTimeUpdate: () => void
  onPlaybackStateChange: () => void
  onCancelExport: () => void
}

type CropDragMode = 'move' | 'resize-nw' | 'resize-ne' | 'resize-sw' | 'resize-se'

interface CropRect {
  x: number
  y: number
  width: number
  height: number
}

interface VideoMetrics {
  width: number
  height: number
  rect: DOMRect
}

const MIN_CROP_SIZE = 16

export default function VideoPlayer({
  videoRef,
  videoUrl,
  isExporting,
  isCropMode,
  isCropActive,
  cropX,
  cropY,
  cropWidth,
  cropHeight,
  onCropChange,
  onDurationChange,
  onTimeUpdate,
  onPlaybackStateChange,
}: VideoPlayerProps) {
  const dragRef = useRef<{
    mode: CropDragMode
    pointerId: number
    startX: number
    startY: number
    startRect: CropRect
    metrics: VideoMetrics
  } | null>(null)

  const [videoRect, setVideoRect] = useState<DOMRect | null>(null)

  const getMetrics = useCallback((): VideoMetrics | null => {
    const vid = videoRef.current
    if (!vid || vid.videoWidth <= 0 || vid.videoHeight <= 0) {
      return null
    }

    return {
      width: vid.videoWidth,
      height: vid.videoHeight,
      rect: vid.getBoundingClientRect(),
    }
  }, [videoRef])

  const clampCrop = useCallback((rect: CropRect, metrics: VideoMetrics): CropRect => {
    const width = Math.max(MIN_CROP_SIZE, Math.min(metrics.width, rect.width))
    const height = Math.max(MIN_CROP_SIZE, Math.min(metrics.height, rect.height))
    const x = Math.max(0, Math.min(metrics.width - width, rect.x))
    const y = Math.max(0, Math.min(metrics.height - height, rect.y))

    return {
      x: Math.round(x),
      y: Math.round(y),
      width: Math.round(width),
      height: Math.round(height),
    }
  }, [])

  const emitCrop = useCallback((rect: CropRect, metrics: VideoMetrics) => {
    const crop = clampCrop(rect, metrics)
    onCropChange({
      cropX: crop.x,
      cropY: crop.y,
      cropWidth: crop.width,
      cropHeight: crop.height,
    })
  }, [clampCrop, onCropChange])

  const ensureInitialCrop = useCallback(() => {
    if (!isCropMode || isCropActive) {
      return
    }

    const metrics = getMetrics()
    if (!metrics) {
      return
    }

    emitCrop({
      x: metrics.width * 0.1,
      y: metrics.height * 0.1,
      width: metrics.width * 0.8,
      height: metrics.height * 0.8,
    }, metrics)
  }, [emitCrop, getMetrics, isCropActive, isCropMode])

  const refreshVideoRect = useCallback(() => {
    const metrics = getMetrics()
    setVideoRect(metrics?.rect ?? null)
  }, [getMetrics])

  useEffect(() => {
    if (!isCropMode) {
      return
    }

    refreshVideoRect()
    ensureInitialCrop()
  }, [ensureInitialCrop, isCropMode, refreshVideoRect])

  useEffect(() => {
    if (!isCropMode) {
      return
    }

    window.addEventListener('resize', refreshVideoRect)
    return () => window.removeEventListener('resize', refreshVideoRect)
  }, [isCropMode, refreshVideoRect])

  const activeCrop = useMemo<CropRect | null>(() => {
    const metrics = getMetrics()
    if (!metrics || !isCropActive || cropWidth <= 0 || cropHeight <= 0) {
      return null
    }

    return clampCrop({
      x: cropX,
      y: cropY,
      width: cropWidth,
      height: cropHeight,
    }, metrics)
  }, [clampCrop, cropHeight, cropWidth, cropX, cropY, getMetrics, isCropActive])

  const overlayStyle = useMemo(() => {
    if (!videoRect) {
      return undefined
    }

    return {
      left: videoRect.left,
      top: videoRect.top,
      width: videoRect.width,
      height: videoRect.height,
    }
  }, [videoRect])

  const cropStyle = useMemo(() => {
    const metrics = getMetrics()
    if (!metrics || !activeCrop) {
      return undefined
    }

    return {
      left: `${(activeCrop.x / metrics.width) * 100}%`,
      top: `${(activeCrop.y / metrics.height) * 100}%`,
      width: `${(activeCrop.width / metrics.width) * 100}%`,
      height: `${(activeCrop.height / metrics.height) * 100}%`,
    }
  }, [activeCrop, getMetrics])

  const handleLoadedMetadata = useCallback(() => {
    onDurationChange()
    refreshVideoRect()
    ensureInitialCrop()
  }, [ensureInitialCrop, onDurationChange, refreshVideoRect])

  const handleCropPointerDown = useCallback((e: ReactPointerEvent, mode: CropDragMode) => {
    const metrics = getMetrics()
    if (!metrics || !activeCrop) {
      return
    }

    dragRef.current = {
      mode,
      pointerId: e.pointerId,
      startX: e.clientX,
      startY: e.clientY,
      startRect: activeCrop,
      metrics,
    }
    const captureTarget = e.currentTarget.closest('[data-crop-region]') as HTMLElement | null
    ;(captureTarget ?? e.currentTarget).setPointerCapture(e.pointerId)
    e.preventDefault()
    e.stopPropagation()
  }, [activeCrop, getMetrics])

  const handleCropPointerMove = useCallback((e: ReactPointerEvent) => {
    const drag = dragRef.current
    if (!drag || drag.pointerId !== e.pointerId) {
      return
    }

    const dx = ((e.clientX - drag.startX) / drag.metrics.rect.width) * drag.metrics.width
    const dy = ((e.clientY - drag.startY) / drag.metrics.rect.height) * drag.metrics.height
    let next = { ...drag.startRect }

    switch (drag.mode) {
      case 'move':
        next.x += dx
        next.y += dy
        break
      case 'resize-nw':
        next.x += dx
        next.y += dy
        next.width -= dx
        next.height -= dy
        break
      case 'resize-ne':
        next.y += dy
        next.width += dx
        next.height -= dy
        break
      case 'resize-sw':
        next.x += dx
        next.width -= dx
        next.height += dy
        break
      case 'resize-se':
        next.width += dx
        next.height += dy
        break
    }

    emitCrop(next, drag.metrics)
    e.preventDefault()
  }, [emitCrop])

  const handleCropPointerUp = useCallback((e: ReactPointerEvent) => {
    if (dragRef.current?.pointerId === e.pointerId) {
      dragRef.current = null
    }
  }, [])

  return (
    <div className="relative flex-1 min-h-0 bg-ve-base flex items-center justify-center overflow-hidden">
      {videoUrl ? (
        <video
          ref={videoRef}
          src={videoUrl}
          preload="auto"
          className="max-w-full max-h-full object-contain"
          onLoadedMetadata={handleLoadedMetadata}
          onDurationChange={handleLoadedMetadata}
          onTimeUpdate={onTimeUpdate}
          onPlay={onPlaybackStateChange}
          onPause={onPlaybackStateChange}
          onEnded={onPlaybackStateChange}
          onResize={refreshVideoRect}
          onError={e => {
            const err = (e.target as HTMLVideoElement).error
            console.error('Video load error:', err?.code, err?.message)
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

      {/* Crop mode overlay */}
      {isCropMode && overlayStyle && cropStyle && (
        <div className="fixed z-20 bg-black/55" style={overlayStyle}>
          <div
            data-crop-region
            className="absolute border-2 border-amber-400/80 bg-white/5 rounded-lg shadow-glow-amber cursor-move"
            style={cropStyle}
            onPointerDown={e => handleCropPointerDown(e, 'move')}
            onPointerMove={handleCropPointerMove}
            onPointerUp={handleCropPointerUp}
            onPointerCancel={handleCropPointerUp}
            role="group"
            aria-label="Crop region"
          >
            <CropHandle className="-left-2 -top-2 cursor-nwse-resize" onPointerDown={e => handleCropPointerDown(e, 'resize-nw')} />
            <CropHandle className="-right-2 -top-2 cursor-nesw-resize" onPointerDown={e => handleCropPointerDown(e, 'resize-ne')} />
            <CropHandle className="-left-2 -bottom-2 cursor-nesw-resize" onPointerDown={e => handleCropPointerDown(e, 'resize-sw')} />
            <CropHandle className="-right-2 -bottom-2 cursor-nwse-resize" onPointerDown={e => handleCropPointerDown(e, 'resize-se')} />
          </div>
        </div>
      )}

      {/* Exporting dim */}
      {isExporting && (
        <div className="absolute inset-0 bg-ve-base/75 backdrop-blur-xs pointer-events-none" />
      )}
    </div>
  )
}

function CropHandle({
  className,
  onPointerDown,
}: {
  className: string
  onPointerDown: (e: ReactPointerEvent) => void
}) {
  return (
    <button
      type="button"
      className={`absolute w-4 h-4 rounded-full bg-amber-400 ring-2 ring-black/40 shadow-glow-amber ${className}`}
      aria-label="Resize crop region"
      onPointerDown={onPointerDown}
    />
  )
}

import { useCallback, useEffect, useRef, useState } from 'react'
import { useReceive, useSend } from './hooks/useBridge'
import type { EditorState, InboundMessage, OutputFormat } from './types/bridge'
import { createRequestId } from './utils/requestId'
import { mergeThumbnailBatch, type ThumbnailRequestIdentity } from './utils/thumbnailBatches'
import { calculateSelectionZoomRange, type TimelineRange } from './utils/timeline'
import Header from './components/Header'
import VideoPlayer from './components/VideoPlayer'
import TransportControls from './components/TransportControls'
import TimelineScrubber from './components/TimelineScrubber'
import ToolPanel from './components/ToolPanel'
import ExportOverlay from './components/ExportOverlay'

const DEFAULT_STATE: EditorState = {
  videoUrl: '',
  ffmpegAvailable: false,
  ffmpegPath: '',
  ffprobeAvailable: false,
  ffprobePath: '',
  runtimeDiagnostics: null,
  watermarkConfig: null,
  theme: 'Dark',
  thumbnails: [],
  duration: 0,
  position: 0,
  isPlaying: false,
  volume: 1,
  isTrimActive: false,
  trimStart: 0,
  trimEnd: 0,
  isCropActive: false,
  isCropMode: false,
  cropX: 0,
  cropY: 0,
  cropWidth: 0,
  cropHeight: 0,
  outputFormat: 'MP4',
  fps: 0,
  qualityScale: 1.0,
  watermarkEnabled: false,
  watermarkText: '',
  watermarkImagePath: '',
  watermarkImageUrl: '',
  availableFormats: ['MP4', 'WebM', 'GIF', 'WebP'],
  isExporting: false,
  exportProgress: 0,
  exportStatusMessage: '',
  activePanel: 'trim',
}

const MIN_TRIM_SECONDS = 0.1
const THUMBNAIL_COUNT = 24

function applyTheme(theme: EditorState['theme']) {
  const el = document.documentElement
  if (theme === 'System') {
    el.removeAttribute('data-theme')
  } else {
    el.setAttribute('data-theme', theme.toLowerCase())
  }
}

export default function App() {
  const [state, setState] = useState<EditorState>(DEFAULT_STATE)
  const [timelineRange, setTimelineRange] = useState<TimelineRange>({ start: 0, end: 0 })
  const [isSelectionZoomed, setIsSelectionZoomed] = useState(false)
  const videoRef = useRef<HTMLVideoElement>(null)
  const activeExportRequestRef = useRef<string | null>(null)
  const terminalExportRequestRef = useRef<string | null>(null)
  const exportResetTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null)
  const activeThumbnailRequestRef = useRef<ThumbnailRequestIdentity | null>(null)
  const thumbnailRevisionRef = useRef(0)
  const send = useSend()
  const canExport = state.ffmpegAvailable && !state.isExporting

  // ── Inbound messages from C# ────────────────────────────────────────────────

  const handleMessage = useCallback((msg: InboundMessage) => {
    switch (msg.type) {
      case 'config':
        applyTheme(msg.theme)
        activeThumbnailRequestRef.current = null
        setTimelineRange({ start: 0, end: 0 })
        setIsSelectionZoomed(false)
        setState(s => ({
          ...s,
          videoUrl: msg.videoUrl,
          thumbnails: [],
          duration: 0,
          position: 0,
          trimStart: 0,
          trimEnd: 0,
          isTrimActive: false,
          theme: msg.theme,
          ffmpegAvailable: msg.ffmpegAvailable,
          ffmpegPath: msg.ffmpegPath ?? '',
          ffprobeAvailable: msg.ffprobeAvailable ?? false,
          ffprobePath: msg.ffprobePath ?? '',
          runtimeDiagnostics: msg.runtimeDiagnostics ?? null,
          watermarkConfig: msg.watermark ?? null,
          watermarkText: msg.watermark?.text ?? '',
          watermarkEnabled: msg.watermark?.enabled ?? false,
          watermarkImagePath: msg.watermark?.imagePath ?? '',
          watermarkImageUrl: msg.watermark?.imageUrl ?? '',
          availableFormats: msg.availableFormats?.length ? msg.availableFormats : ['MP4', 'WebM', 'GIF', 'WebP'],
          outputFormat: msg.availableFormats?.includes(s.outputFormat)
            ? s.outputFormat
            : (msg.availableFormats?.[0] ?? s.outputFormat),
        }))
        break

      case 'watermarkImageSelected':
        setState(s => ({
          ...s,
          watermarkImagePath: msg.path,
          watermarkImageUrl: msg.imageUrl,
          watermarkEnabled: s.watermarkEnabled || !!msg.path,
        }))
        break

      case 'thumbnailBatch':
        setState(s => ({
          ...s,
          thumbnails: mergeThumbnailBatch(s.thumbnails, activeThumbnailRequestRef.current, msg),
        }))
        break

      case 'exportProgress':
        if (msg.requestId !== activeExportRequestRef.current) break
        setState(s => ({
          ...s,
          isExporting: true,
          exportProgress: msg.percent,
          exportStatusMessage: msg.message,
        }))
        break

      case 'exportComplete':
        if (msg.requestId !== activeExportRequestRef.current) break
        activeExportRequestRef.current = null
        terminalExportRequestRef.current = msg.requestId
        setState(s => ({
          ...s,
          isExporting: false,
          exportProgress: 100,
          exportStatusMessage: 'Done!',
        }))
        if (exportResetTimerRef.current) clearTimeout(exportResetTimerRef.current)
        exportResetTimerRef.current = setTimeout(() => {
          if (terminalExportRequestRef.current === msg.requestId && activeExportRequestRef.current === null) {
            setState(s => ({ ...s, exportProgress: 0, exportStatusMessage: '' }))
          }
        }, 2000)
        break

      case 'exportCancelled':
        if (msg.requestId !== activeExportRequestRef.current) break
        activeExportRequestRef.current = null
        terminalExportRequestRef.current = msg.requestId
        setState(s => ({ ...s, isExporting: false, exportProgress: 0, exportStatusMessage: 'Cancelled' }))
        break

      case 'exportError':
        if (msg.requestId !== activeExportRequestRef.current) break
        activeExportRequestRef.current = null
        terminalExportRequestRef.current = msg.requestId
        setState(s => ({ ...s, isExporting: false, exportProgress: 0, exportStatusMessage: msg.message || 'Export failed' }))
        break

      case 'bridgeError':
        if (msg.requestId && msg.requestId === activeExportRequestRef.current) {
          activeExportRequestRef.current = null
          terminalExportRequestRef.current = msg.requestId
          setState(s => ({
            ...s,
            isExporting: false,
            exportProgress: 0,
            exportStatusMessage: msg.message || 'The editor host rejected the export request.',
          }))
        } else if (msg.requestId && msg.requestId === activeThumbnailRequestRef.current?.requestId) {
          activeThumbnailRequestRef.current = null
          setState(s => ({
            ...s,
            thumbnails: [],
            exportStatusMessage: `Thumbnails unavailable: ${msg.message}`,
          }))
        } else {
          setState(s => ({ ...s, exportStatusMessage: msg.message || 'The editor host rejected a request.' }))
        }
        break
    }
  }, [])

  useReceive(handleMessage)

  // ── Tell C# we're ready once mounted ───────────────────────────────────────

  useEffect(() => {
    send({ type: 'ready', protocolVersion: 2 })
  }, [send])

  useEffect(() => () => {
    if (exportResetTimerRef.current) clearTimeout(exportResetTimerRef.current)
  }, [])

  // ── Sync video element duration once loaded ─────────────────────────────────

  const onVideoDurationChange = useCallback(() => {
    const vid = videoRef.current
    if (!vid || !isFinite(vid.duration)) return
    setState(s => ({
      ...s,
      duration: vid.duration,
      trimEnd: s.trimEnd === 0 ? vid.duration : s.trimEnd,
    }))
    if (!isSelectionZoomed) {
      setTimelineRange({ start: 0, end: vid.duration })
    }
  }, [isSelectionZoomed])

  // ── Progressive thumbnails for the currently visible timeline range ───────

  useEffect(() => {
    if (!state.videoUrl || state.duration <= 0 || timelineRange.end <= timelineRange.start) return

    thumbnailRevisionRef.current += 1
    const requestId = createRequestId()
    const revision = thumbnailRevisionRef.current
    activeThumbnailRequestRef.current = { requestId, revision }
    setState(s => ({ ...s, thumbnails: Array<string | null>(THUMBNAIL_COUNT).fill(null) }))

    const delivered = send({
      type: 'requestThumbnails',
      requestId,
      revision,
      startTime: timelineRange.start,
      endTime: timelineRange.end,
      count: THUMBNAIL_COUNT,
    })

    if (!delivered && activeThumbnailRequestRef.current?.requestId === requestId) {
      activeThumbnailRequestRef.current = null
      setState(s => ({ ...s, thumbnails: [] }))
    }
  }, [send, state.duration, state.videoUrl, timelineRange.end, timelineRange.start])

  const onVideoTimeUpdate = useCallback(() => {
    const vid = videoRef.current
    if (!vid) return

    if (state.isTrimActive && !vid.paused && vid.currentTime >= state.trimEnd) {
      vid.pause()
      vid.currentTime = state.trimEnd
    }

    setState(s => ({ ...s, position: vid.currentTime }))
  }, [state.isTrimActive, state.trimEnd])

  const syncPlaybackState = useCallback(() => {
    const vid = videoRef.current
    if (!vid) return

    const isPlaying = !vid.paused && !vid.ended
    const position = isFinite(vid.currentTime) ? vid.currentTime : 0

    setState(s => {
      if (s.isPlaying === isPlaying && s.position === position) {
        return s
      }

      return {
        ...s,
        isPlaying,
        position,
      }
    })
  }, [])

  // ── Playback controls ───────────────────────────────────────────────────────

  const togglePlayPause = useCallback(() => {
    const vid = videoRef.current
    if (!vid) return
    if (vid.paused) {
      const rangeStart = state.isTrimActive ? state.trimStart : 0
      const rangeEnd = state.isTrimActive ? state.trimEnd : state.duration
      if (vid.currentTime < rangeStart || vid.currentTime >= rangeEnd - 0.01) {
        vid.currentTime = rangeStart
        setState(s => ({ ...s, position: rangeStart }))
      }

      vid.play().catch(err => {
        console.error('Play failed:', err)
        setState(s => ({ ...s, isPlaying: false }))
      })
    } else {
      vid.pause()
    }
  }, [state.duration, state.isTrimActive, state.trimEnd, state.trimStart])

  const skipBy = useCallback((seconds: number) => {
    const vid = videoRef.current
    if (!vid) return

    const rangeStart = state.isTrimActive ? state.trimStart : 0
    const rangeEnd = state.isTrimActive ? state.trimEnd : state.duration
    const next = Math.max(rangeStart, Math.min(rangeEnd, vid.currentTime + seconds))
    vid.currentTime = next
    setState(s => ({ ...s, position: next }))
  }, [state.duration, state.isTrimActive, state.trimEnd, state.trimStart])

  const skipBack = useCallback(() => {
    skipBy(-5)
  }, [skipBy])

  const skipForward = useCallback(() => {
    skipBy(5)
  }, [skipBy])

  const seekTo = useCallback((seconds: number) => {
    const rangeStart = state.isTrimActive ? state.trimStart : 0
    const rangeEnd = state.isTrimActive ? state.trimEnd : state.duration
    const next = Math.max(rangeStart, Math.min(rangeEnd, seconds))
    const vid = videoRef.current
    if (vid) vid.currentTime = next
    setState(s => ({ ...s, position: next }))
  }, [state.duration, state.isTrimActive, state.trimEnd, state.trimStart])

  const setVolume = useCallback((v: number) => {
    const vid = videoRef.current
    if (vid) vid.volume = v
    setState(s => ({ ...s, volume: v }))
  }, [])

  const setTrimStart = useCallback((value: number) => {
    const trimEnd = state.trimEnd > 0 ? state.trimEnd : state.duration
    const maxStart = Math.max(0, trimEnd - MIN_TRIM_SECONDS)
    const next = Math.max(0, Math.min(value, maxStart))
    const vid = videoRef.current
    if (vid) vid.currentTime = next
    setState(s => ({ ...s, trimStart: next, position: next, isTrimActive: true }))
  }, [state.duration, state.trimEnd])

  const setTrimEnd = useCallback((value: number) => {
    const minEnd = Math.min(state.duration, state.trimStart + MIN_TRIM_SECONDS)
    const next = Math.max(minEnd, Math.min(value, state.duration))
    const vid = videoRef.current
    if (vid) vid.currentTime = next
    setState(s => ({ ...s, trimEnd: next, position: next, isTrimActive: true }))
  }, [state.duration, state.trimStart])

  // ── Export ──────────────────────────────────────────────────────────────────

  const requestExport = useCallback(() => {
    if (state.isExporting || !state.ffmpegAvailable) {
      return
    }

    const requestId = createRequestId()
    activeExportRequestRef.current = requestId
    terminalExportRequestRef.current = null
    const delivered = send({
      type: 'requestExport',
      requestId,
      isTrimActive: state.isTrimActive,
      trimStart: state.trimStart,
      trimEnd: state.trimEnd,
      isCropActive: state.isCropActive,
      cropX: state.cropX,
      cropY: state.cropY,
      cropWidth: state.cropWidth,
      cropHeight: state.cropHeight,
      outputFormat: state.outputFormat as OutputFormat,
      fps: state.fps,
      qualityScale: state.qualityScale,
      watermarkEnabled: state.watermarkEnabled,
      watermarkText: state.watermarkText,
      watermarkImagePath: state.watermarkImagePath,
    })
    if (!delivered && activeExportRequestRef.current === requestId) {
      activeExportRequestRef.current = null
    }
    setState(s => ({
      ...s,
      isExporting: delivered,
      exportProgress: 0,
      exportStatusMessage: delivered ? 'Preparing…' : 'Could not contact the editor host. Please retry.',
    }))
  }, [send, state])

  const cancelExport = useCallback(() => {
    const requestId = activeExportRequestRef.current
    if (requestId) send({ type: 'cancelExport', requestId })
  }, [send])

  const zoomToSelection = useCallback(() => {
    if (!state.isTrimActive) return
    const nextRange = calculateSelectionZoomRange(state.duration, state.trimStart, state.trimEnd)
    if (nextRange.start === timelineRange.start && nextRange.end === timelineRange.end) {
      setIsSelectionZoomed(nextRange.start > 0 || nextRange.end < state.duration)
      return
    }

    activeThumbnailRequestRef.current = null
    setTimelineRange(nextRange)
    setIsSelectionZoomed(nextRange.start > 0 || nextRange.end < state.duration)
  }, [state.duration, state.isTrimActive, state.trimEnd, state.trimStart, timelineRange.end, timelineRange.start])

  const resetTimelineZoom = useCallback(() => {
    if (timelineRange.start === 0 && timelineRange.end === state.duration) {
      setIsSelectionZoomed(false)
      return
    }

    activeThumbnailRequestRef.current = null
    setTimelineRange({ start: 0, end: state.duration })
    setIsSelectionZoomed(false)
  }, [state.duration, timelineRange.end, timelineRange.start])

  const resetTrim = useCallback(() => {
    setState(s => ({ ...s, isTrimActive: false, trimStart: 0, trimEnd: s.duration }))
    resetTimelineZoom()
  }, [resetTimelineZoom])

  // ── Keyboard shortcuts ──────────────────────────────────────────────────────

  useEffect(() => {
    const handler = (e: KeyboardEvent) => {
      const target = e.target
      if (
        target instanceof HTMLInputElement ||
        target instanceof HTMLTextAreaElement ||
        target instanceof HTMLSelectElement ||
        target instanceof HTMLButtonElement ||
        (target instanceof Element && target.closest('[contenteditable="true"],[role="button"],[role="switch"]'))
      ) {
        return
      }

      const key = e.key.toLowerCase()
      switch (key) {
        case ' ':        e.preventDefault(); togglePlayPause(); break
        case 'arrowleft': e.preventDefault(); skipBy(e.altKey ? -0.2 : e.shiftKey ? -5 : -1); break
        case 'arrowright': e.preventDefault(); skipBy(e.altKey ? 0.2 : e.shiftKey ? 5 : 1); break
        case 'i': setTrimStart(state.position); break
        case 'o': setTrimEnd(state.position); break
        case 'e':
        case 's': if (e.ctrlKey) { e.preventDefault(); if (canExport) requestExport() } break
      }
    }
    window.addEventListener('keydown', handler)
    return () => window.removeEventListener('keydown', handler)
  }, [canExport, requestExport, setTrimEnd, setTrimStart, skipBy, state.position, togglePlayPause])

  return (
    <div className="relative flex flex-col w-full h-full bg-ve-base select-none overflow-hidden">
      <Header
        videoPath={state.videoUrl}
        isExporting={state.isExporting}
        exportProgress={state.exportProgress}
        exportStatusMessage={state.exportStatusMessage}
        ffmpegAvailable={state.ffmpegAvailable}
        ffmpegPath={state.ffmpegPath}
        runtimeDiagnostics={state.runtimeDiagnostics}
        onExport={requestExport}
      />

      <div className="flex flex-1 min-h-0">
        <div className="flex flex-col flex-1 min-w-0">
          <VideoPlayer
            videoRef={videoRef}
            videoUrl={state.videoUrl}
            isExporting={state.isExporting}
            exportProgress={state.exportProgress}
            exportStatusMessage={state.exportStatusMessage}
            isCropMode={state.isCropMode}
            isCropActive={state.isCropActive}
            cropX={state.cropX}
            cropY={state.cropY}
            cropWidth={state.cropWidth}
            cropHeight={state.cropHeight}
            watermarkEnabled={state.watermarkEnabled}
            watermarkText={state.watermarkText}
            watermarkImageUrl={state.watermarkImageUrl}
            watermarkPositionX={state.watermarkConfig?.positionX ?? 0.95}
            watermarkPositionY={state.watermarkConfig?.positionY ?? 0.95}
            watermarkOpacity={state.watermarkConfig?.opacity ?? 0.8}
            onCropChange={crop => setState(s => ({ ...s, ...crop, isCropActive: true }))}
            onDurationChange={onVideoDurationChange}
            onTimeUpdate={onVideoTimeUpdate}
            onPlaybackStateChange={syncPlaybackState}
            onCancelExport={cancelExport}
          />

          <TransportControls
            position={state.position}
            duration={state.duration}
            isPlaying={state.isPlaying}
            volume={state.volume}
            onPlayPause={togglePlayPause}
            onSkipBack={skipBack}
            onSkipForward={skipForward}
            onVolumeChange={setVolume}
          />

          <TimelineScrubber
            duration={state.duration}
            position={state.position}
            trimStart={state.trimStart}
            trimEnd={state.trimEnd}
            isTrimActive={state.isTrimActive}
            thumbnails={state.thumbnails}
            viewStart={timelineRange.start}
            viewEnd={timelineRange.end || state.duration}
            isSelectionZoomed={isSelectionZoomed}
            onSeek={seekTo}
            onTrimStartChange={setTrimStart}
            onTrimEndChange={setTrimEnd}
            onSetTrimStart={() => setTrimStart(state.position)}
            onSetTrimEnd={() => setTrimEnd(state.position)}
            onResetTrim={resetTrim}
            onZoomToSelection={zoomToSelection}
            onResetZoom={resetTimelineZoom}
          />
        </div>

        <ToolPanel
          state={state}
          onStateChange={patch => setState(s => ({ ...s, ...patch }))}
          onResetCrop={() => setState(s => ({
            ...s,
            isCropActive: false,
            isCropMode: false,
            cropX: 0,
            cropY: 0,
            cropWidth: 0,
            cropHeight: 0,
          }))}
          onExport={requestExport}
          onPickWatermarkImage={() => send({ type: 'requestWatermarkImage' })}
        />
      </div>

      {state.isExporting && (
        <ExportOverlay
          progress={state.exportProgress}
          message={state.exportStatusMessage}
          onCancel={cancelExport}
        />
      )}
    </div>
  )
}

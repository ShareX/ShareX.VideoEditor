import { useCallback, useEffect, useRef, useState } from 'react'
import { useReceive, useSend } from './hooks/useBridge'
import type { EditorState, InboundMessage, OutputFormat } from './types/bridge'
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
  fps: 30,
  qualityScale: 1.0,
  watermarkEnabled: false,
  watermarkText: '',
  isExporting: false,
  exportProgress: 0,
  exportStatusMessage: '',
  activePanel: 'trim',
}

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
  const videoRef = useRef<HTMLVideoElement>(null)
  const send = useSend()

  // ── Inbound messages from C# ────────────────────────────────────────────────

  const handleMessage = useCallback((msg: InboundMessage) => {
    switch (msg.type) {
      case 'config':
        applyTheme(msg.theme)
        setState(s => ({
          ...s,
          videoUrl: msg.videoUrl,
          theme: msg.theme,
          ffmpegAvailable: msg.ffmpegAvailable,
          ffmpegPath: msg.ffmpegPath ?? '',
          runtimeDiagnostics: msg.runtimeDiagnostics ?? null,
          watermarkConfig: msg.watermark ?? null,
          watermarkText: msg.watermark?.text ?? '',
          watermarkEnabled: msg.watermark?.enabled ?? false,
        }))
        break

      case 'thumbnails':
        setState(s => ({ ...s, thumbnails: msg.frames }))
        break

      case 'exportProgress':
        setState(s => ({
          ...s,
          isExporting: true,
          exportProgress: msg.percent,
          exportStatusMessage: msg.message,
        }))
        break

      case 'exportComplete':
        setState(s => ({
          ...s,
          isExporting: false,
          exportProgress: 100,
          exportStatusMessage: 'Done!',
        }))
        // Brief display, then reset
        setTimeout(() => setState(s => ({ ...s, exportProgress: 0, exportStatusMessage: '' })), 2000)
        break

      case 'exportCancelled':
        setState(s => ({ ...s, isExporting: false, exportProgress: 0, exportStatusMessage: 'Cancelled' }))
        break

      case 'exportError':
        setState(s => ({ ...s, isExporting: false, exportProgress: 0, exportStatusMessage: 'Export failed' }))
        break
    }
  }, [])

  useReceive(handleMessage)

  // ── Tell C# we're ready once mounted ───────────────────────────────────────

  useEffect(() => {
    send({ type: 'ready' })
  }, [send])

  // ── Sync video element duration once loaded ─────────────────────────────────

  const onVideoDurationChange = useCallback(() => {
    const vid = videoRef.current
    if (!vid || !isFinite(vid.duration)) return
    setState(s => ({
      ...s,
      duration: vid.duration,
      trimEnd: s.trimEnd === 0 ? vid.duration : s.trimEnd,
    }))
  }, [])

  const onVideoTimeUpdate = useCallback(() => {
    const vid = videoRef.current
    if (vid) setState(s => ({ ...s, position: vid.currentTime }))
  }, [])

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
      vid.play().catch(() => {
        setState(s => ({ ...s, isPlaying: false }))
      })
    } else {
      vid.pause()
    }
  }, [])

  const skipBack = useCallback(() => {
    const vid = videoRef.current
    if (vid) vid.currentTime = Math.max(0, vid.currentTime - 5)
  }, [])

  const skipForward = useCallback(() => {
    const vid = videoRef.current
    if (vid) vid.currentTime = Math.min(state.duration, vid.currentTime + 5)
  }, [state.duration])

  const seekTo = useCallback((seconds: number) => {
    const vid = videoRef.current
    if (vid) vid.currentTime = seconds
    setState(s => ({ ...s, position: seconds }))
  }, [])

  const setVolume = useCallback((v: number) => {
    const vid = videoRef.current
    if (vid) vid.volume = v
    setState(s => ({ ...s, volume: v }))
  }, [])

  // ── Export ──────────────────────────────────────────────────────────────────

  const requestExport = useCallback(() => {
    send({
      type: 'requestExport',
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
    })
    setState(s => ({ ...s, isExporting: true, exportProgress: 0, exportStatusMessage: 'Preparing…' }))
  }, [send, state])

  const cancelExport = useCallback(() => {
    send({ type: 'cancelExport' })
  }, [send])

  // ── Keyboard shortcuts ──────────────────────────────────────────────────────

  useEffect(() => {
    const handler = (e: KeyboardEvent) => {
      if (e.target instanceof HTMLInputElement || e.target instanceof HTMLTextAreaElement) return
      switch (e.key) {
        case ' ':        e.preventDefault(); togglePlayPause(); break
        case 'ArrowLeft': e.preventDefault(); skipBack(); break
        case 'ArrowRight': e.preventDefault(); skipForward(); break
        case 'i': setState(s => ({ ...s, isTrimActive: true, trimStart: s.position })); break
        case 'o': setState(s => ({ ...s, isTrimActive: true, trimEnd: s.position })); break
        case 'e': if (e.ctrlKey) { e.preventDefault(); if (!state.isExporting) requestExport() } break
      }
    }
    window.addEventListener('keydown', handler)
    return () => window.removeEventListener('keydown', handler)
  }, [togglePlayPause, skipBack, skipForward, requestExport, state.isExporting])

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
            onSeek={seekTo}
            onTrimStartChange={v => setState(s => ({ ...s, trimStart: v, isTrimActive: true }))}
            onTrimEndChange={v => setState(s => ({ ...s, trimEnd: v, isTrimActive: true }))}
            onSetTrimStart={() => setState(s => ({ ...s, trimStart: s.position, isTrimActive: true }))}
            onSetTrimEnd={() => setState(s => ({ ...s, trimEnd: s.position, isTrimActive: true }))}
            onResetTrim={() => setState(s => ({ ...s, isTrimActive: false, trimStart: 0, trimEnd: s.duration }))}
          />
        </div>

        <ToolPanel
          state={state}
          onStateChange={patch => setState(s => ({ ...s, ...patch }))}
          onExport={requestExport}
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

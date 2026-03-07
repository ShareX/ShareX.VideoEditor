import type { ReactNode } from 'react'
import type { ActivePanel, EditorState, OutputFormat } from '../types/bridge'

const OUTPUT_FORMATS: OutputFormat[] = ['MP4', 'WebM', 'GIF', 'WebP']

interface ToolPanelProps {
  state: EditorState
  onStateChange: (patch: Partial<EditorState>) => void
  onExport: () => void
}

export default function ToolPanel({ state, onStateChange, onExport }: ToolPanelProps) {
  const tab = (label: string, panel: ActivePanel) => (
    <button
      key={panel}
      onClick={() => onStateChange({ activePanel: panel })}
      className={`px-3 py-1.5 text-xs font-medium rounded-md transition-colors ${
        state.activePanel === panel
          ? 'bg-ve-accent text-white'
          : 'text-ve-secondary hover:text-ve-text hover:bg-ve-elevated'
      }`}
    >
      {label}
    </button>
  )

  return (
    <aside className="flex flex-col w-72 shrink-0 bg-ve-surface border-l border-ve-border">
      {/* Tab bar */}
      <div className="flex items-center gap-1 p-2 border-b border-ve-border">
        {tab('Trim', 'trim')}
        {tab('Crop', 'crop')}
        {tab('Watermark', 'watermark')}
        {tab('Export', 'export')}
      </div>

      {/* Panel content */}
      <div className="flex-1 overflow-y-auto p-4 space-y-4">
        {state.activePanel === 'trim' && <TrimPanel state={state} onChange={onStateChange} />}
        {state.activePanel === 'crop' && <CropPanel state={state} onChange={onStateChange} />}
        {state.activePanel === 'watermark' && <WatermarkPanel state={state} onChange={onStateChange} />}
        {state.activePanel === 'export' && <ExportSettingsPanel state={state} onChange={onStateChange} />}
      </div>

      {/* Primary export button */}
      <div className="p-4 border-t border-ve-border">
        <button
          onClick={onExport}
          disabled={state.isExporting || !state.ffmpegAvailable}
          className="w-full py-2.5 text-sm font-semibold rounded-lg bg-ve-accent text-white
                     hover:bg-ve-accent-h active:scale-[0.98] transition-all
                     disabled:opacity-40 disabled:cursor-not-allowed"
        >
          Export Video
        </button>
      </div>
    </aside>
  )
}

// ── Sub-panels ────────────────────────────────────────────────────────────────

function SectionHeader({ children }: { children: ReactNode }) {
  return (
    <p className="text-[10px] font-semibold tracking-widest text-ve-muted uppercase mb-2">
      {children}
    </p>
  )
}

function Row({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div className="flex items-center justify-between gap-2">
      <span className="text-xs text-ve-secondary shrink-0">{label}</span>
      <span className="text-xs font-mono text-ve-text">{children}</span>
    </div>
  )
}

function TrimPanel({ state }: { state: EditorState; onChange?: (p: Partial<EditorState>) => void }) {
  const formatTime = (s: number) => {
    const m = Math.floor(s / 60)
    const sec = Math.floor(s % 60)
    return `${m}:${String(sec).padStart(2, '0')}`
  }
  const dur = state.isTrimActive ? state.trimEnd - state.trimStart : state.duration

  return (
    <>
      <SectionHeader>Trim</SectionHeader>
      {state.isTrimActive ? (
        <div className="space-y-2">
          <Row label="In">{formatTime(state.trimStart)}</Row>
          <Row label="Out">{formatTime(state.trimEnd)}</Row>
          <Row label="Duration">{formatTime(dur)}</Row>
        </div>
      ) : (
        <p className="text-xs text-ve-muted">
          Drag the trim handles on the timeline, or use [ Set In ] / [ Set Out ] buttons.
        </p>
      )}
    </>
  )
}

function CropPanel({ state, onChange }: { state: EditorState; onChange: (p: Partial<EditorState>) => void }) {
  return (
    <>
      <SectionHeader>Crop</SectionHeader>
      <button
        onClick={() => onChange({ isCropMode: !state.isCropMode })}
        className="w-full py-2 text-xs font-medium rounded-lg bg-ve-accent text-white hover:bg-ve-accent-h transition-colors"
      >
        {state.isCropMode ? 'Exit Crop Mode' : 'Enter Crop Mode'}
      </button>
      <p className="text-xs text-ve-muted mt-2">
        Enter crop mode and drag the crop region on the video.
      </p>
      {state.isCropActive && (
        <div className="space-y-1 mt-3">
          <Row label="X">{state.cropX}px</Row>
          <Row label="Y">{state.cropY}px</Row>
          <Row label="W">{state.cropWidth}px</Row>
          <Row label="H">{state.cropHeight}px</Row>
        </div>
      )}
    </>
  )
}

function WatermarkPanel({ state, onChange }: { state: EditorState; onChange: (p: Partial<EditorState>) => void }) {
  return (
    <>
      <SectionHeader>Watermark</SectionHeader>
      <label className="flex items-center gap-2 cursor-pointer">
        <input
          type="checkbox"
          checked={state.watermarkEnabled}
          onChange={e => onChange({ watermarkEnabled: e.target.checked })}
          className="accent-ve-accent"
        />
        <span className="text-xs text-ve-text">Enable watermark</span>
      </label>
      {state.watermarkEnabled && (
        <div className="mt-3 space-y-2">
          <SectionHeader>Text</SectionHeader>
          <input
            type="text"
            value={state.watermarkText}
            onChange={e => onChange({ watermarkText: e.target.value })}
            placeholder="Watermark text…"
            className="w-full bg-ve-elevated text-ve-text text-xs rounded-md px-3 py-2
                       border border-ve-border focus:outline-none focus:border-ve-accent placeholder:text-ve-muted"
          />
        </div>
      )}
    </>
  )
}

function ExportSettingsPanel({ state, onChange }: { state: EditorState; onChange: (p: Partial<EditorState>) => void }) {
  return (
    <>
      <SectionHeader>Export Settings</SectionHeader>

      {/* Format */}
      <div className="space-y-1">
        <p className="text-[11px] text-ve-secondary">Format</p>
        <select
          value={state.outputFormat}
          onChange={e => onChange({ outputFormat: e.target.value as OutputFormat })}
          className="w-full bg-ve-elevated text-ve-text text-xs rounded-md px-3 py-2
                     border border-ve-border focus:outline-none focus:border-ve-accent"
        >
          {OUTPUT_FORMATS.map(f => (
            <option key={f} value={f}>{f}</option>
          ))}
        </select>
      </div>

      {/* FPS */}
      <div className="space-y-1">
        <div className="flex justify-between">
          <p className="text-[11px] text-ve-secondary">Frame Rate</p>
          <span className="text-[11px] font-mono text-ve-text">{state.fps} fps</span>
        </div>
        <input
          type="range"
          min={1}
          max={60}
          step={1}
          value={state.fps}
          onChange={e => onChange({ fps: parseInt(e.target.value) })}
          className="w-full"
        />
      </div>

      {/* Quality Scale */}
      <div className="space-y-1">
        <div className="flex justify-between">
          <p className="text-[11px] text-ve-secondary">Quality Scale</p>
          <span className="text-[11px] font-mono text-ve-text">{Math.round(state.qualityScale * 100)}%</span>
        </div>
        <input
          type="range"
          min={0.25}
          max={1}
          step={0.05}
          value={state.qualityScale}
          onChange={e => onChange({ qualityScale: parseFloat(e.target.value) })}
          className="w-full"
        />
      </div>
    </>
  )
}

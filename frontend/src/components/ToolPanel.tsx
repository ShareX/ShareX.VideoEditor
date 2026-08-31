import {
  Scissors, Crop, Type, Settings,
  Move, RotateCcw, Download,
} from 'lucide-react'
import type { ActivePanel, EditorState, OutputFormat } from '../types/bridge'
import { formatPreciseTime } from '../utils/time'
import {
  PremiumButton, PremiumInput, PremiumSelect,
  PremiumSlider, PremiumToggle, SectionLabel, FieldRow,
} from './ui'

const OUTPUT_FORMATS: OutputFormat[] = ['MP4', 'WebM', 'GIF', 'WebP']

interface ToolPanelProps {
  state: EditorState
  onStateChange: (patch: Partial<EditorState>) => void
  onResetCrop: () => void
  onExport: () => void
  onPickWatermarkImage: () => void
}

const TABS: { id: ActivePanel; label: string; icon: typeof Scissors }[] = [
  { id: 'trim', label: 'Trim', icon: Scissors },
  { id: 'crop', label: 'Crop', icon: Crop },
  { id: 'watermark', label: 'Text', icon: Type },
  { id: 'export', label: 'Export', icon: Settings },
]

export default function ToolPanel({ state, onStateChange, onResetCrop, onExport, onPickWatermarkImage }: ToolPanelProps) {
  return (
    <aside className="flex flex-col w-72 shrink-0 bg-ve-surface/60 backdrop-blur-md border-l border-white/6">
      {/* Segmented tab bar */}
      <div className="p-3">
        <div className="flex items-center gap-0.5 p-1 rounded-2xl bg-ve-elevated/50 ring-1 ring-white/6">
          {TABS.map(({ id, label, icon: Icon }) => {
            const active = state.activePanel === id
            return (
              <button
                key={id}
                onClick={() => onStateChange({ activePanel: id })}
                className={`
                  flex-1 flex items-center justify-center gap-1.5
                  h-8 rounded-xl text-xs font-medium
                  transition-all duration-200 ease-out
                  ${active
                    ? 'bg-ve-accent/15 text-amber-400 ring-1 ring-amber-400/40 shadow-glow-amber-sm'
                    : 'text-ve-secondary hover:text-ve-text hover:bg-white/5'
                  }
                `}
              >
                <Icon className="w-3.5 h-3.5" />
                <span>{label}</span>
              </button>
            )
          })}
        </div>
      </div>

      {/* Panel content */}
      <div className="flex-1 overflow-y-auto px-4 pb-4 space-y-5">
        <div className="animate-slide-up">
          {state.activePanel === 'trim' && <TrimPanel state={state} />}
          {state.activePanel === 'crop' && (
            <CropPanel state={state} onChange={onStateChange} onReset={onResetCrop} />
          )}
          {state.activePanel === 'watermark' && (
            <WatermarkPanel state={state} onChange={onStateChange} onPickImage={onPickWatermarkImage} />
          )}
          {state.activePanel === 'export' && <ExportSettingsPanel state={state} onChange={onStateChange} />}
        </div>
      </div>

      {/* Primary export button */}
      <div className="p-4 border-t border-white/6">
        <PremiumButton
          onClick={onExport}
          disabled={state.isExporting || !state.ffmpegAvailable}
          variant="primary"
          size="lg"
          className="w-full"
          icon={<Download className="w-4 h-4" />}
        >
          Export Video
        </PremiumButton>
      </div>
    </aside>
  )
}

// ── Sub-panels ────────────────────────────────────────────────────────────────

function TrimPanel({ state }: { state: EditorState }) {
  const dur = state.isTrimActive ? state.trimEnd - state.trimStart : state.duration

  return (
    <>
      <SectionLabel>Trim</SectionLabel>
      {state.isTrimActive ? (
        <div className="space-y-2.5 p-3 rounded-2xl bg-ve-elevated/40 ring-1 ring-white/6">
          <FieldRow label="In">{formatPreciseTime(state.trimStart)}</FieldRow>
          <FieldRow label="Out">{formatPreciseTime(state.trimEnd)}</FieldRow>
          <div className="h-px bg-white/6" />
          <FieldRow label="Duration">{formatPreciseTime(dur)}</FieldRow>
        </div>
      ) : (
        <div className="flex flex-col items-center gap-3 py-6 text-center">
          <Scissors className="w-8 h-8 text-ve-muted/50" />
          <p className="text-xs text-ve-muted leading-relaxed max-w-[200px]">
            Drag the trim handles on the timeline, or use the Set In / Set Out buttons below.
          </p>
        </div>
      )}
    </>
  )
}

function CropPanel({
  state,
  onChange,
  onReset,
}: {
  state: EditorState
  onChange: (p: Partial<EditorState>) => void
  onReset: () => void
}) {
  return (
    <>
      <SectionLabel>Crop</SectionLabel>
      <PremiumButton
        onClick={() => onChange({ isCropMode: !state.isCropMode })}
        variant={state.isCropMode ? 'primary' : 'secondary'}
        size="md"
        className="w-full"
        icon={state.isCropMode ? <RotateCcw className="w-3.5 h-3.5" /> : <Move className="w-3.5 h-3.5" />}
      >
        {state.isCropMode ? 'Exit Crop Mode' : 'Enter Crop Mode'}
      </PremiumButton>
      <p className="text-xs text-ve-muted leading-relaxed mt-2">
        {state.isCropActive
          ? 'Crop is applied to export. Enter crop mode to adjust it, or reset to export the full frame.'
          : 'Enter crop mode and drag the crop region on the video preview.'}
      </p>
      {state.isCropActive && (
        <>
          <div className="space-y-2 mt-3 p-3 rounded-2xl bg-ve-elevated/40 ring-1 ring-white/6">
            <FieldRow label="X">{state.cropX}px</FieldRow>
            <FieldRow label="Y">{state.cropY}px</FieldRow>
            <FieldRow label="W">{state.cropWidth}px</FieldRow>
            <FieldRow label="H">{state.cropHeight}px</FieldRow>
          </div>
          <PremiumButton
            onClick={onReset}
            variant="ghost"
            size="md"
            className="w-full mt-3"
            icon={<RotateCcw className="w-3.5 h-3.5" />}
          >
            Reset Crop
          </PremiumButton>
        </>
      )}
    </>
  )
}

function WatermarkPanel({
  state,
  onChange,
  onPickImage,
}: {
  state: EditorState
  onChange: (p: Partial<EditorState>) => void
  onPickImage: () => void
}) {
  const imageName = state.watermarkImagePath
    ? state.watermarkImagePath.split(/[\\/]/).pop()
    : ''

  return (
    <>
      <SectionLabel>Watermark</SectionLabel>
      <PremiumToggle
        checked={state.watermarkEnabled}
        onChange={checked => onChange({ watermarkEnabled: checked })}
        label="Enable watermark"
      />
      {state.watermarkEnabled && (
        <div className="mt-4 space-y-3">
          <PremiumInput
            label="Text"
            value={state.watermarkText}
            onChange={e => onChange({ watermarkText: e.target.value })}
            placeholder="Watermark text…"
          />
          <PremiumButton
            onClick={onPickImage}
            variant="secondary"
            size="md"
            className="w-full"
          >
            {imageName ? 'Change image' : 'Choose image'}
          </PremiumButton>
          {imageName && (
            <FieldRow label="Image">{imageName}</FieldRow>
          )}
        </div>
      )}
    </>
  )
}

function ExportSettingsPanel({ state, onChange }: { state: EditorState; onChange: (p: Partial<EditorState>) => void }) {
  return (
    <>
      <SectionLabel>Export Settings</SectionLabel>

      <PremiumInput
        label="FFmpeg Path"
        value={state.ffmpegPath || '(not set)'}
        readOnly
        title={state.ffmpegPath || 'VideoEditorOptions.FFmpegPath was not provided by the host.'}
      />

      <PremiumInput
        label="FFprobe Path"
        value={state.ffprobePath || '(not set)'}
        readOnly
        title={
          state.ffprobeAvailable
            ? state.ffprobePath
            : 'VideoEditorOptions.FFprobePath was not provided by the host or could not be resolved.'
        }
      />

      <PremiumSelect
        label="Format"
        value={state.outputFormat}
        onChange={e => onChange({ outputFormat: e.target.value as OutputFormat })}
        options={(state.availableFormats.length > 0 ? state.availableFormats : OUTPUT_FORMATS).map(f => ({
          value: f,
          label: f,
        }))}
      />

      <PremiumSlider
        label="Frame Rate"
        displayValue={state.fps > 0 ? `${state.fps} fps` : 'Source'}
        min={0}
        max={60}
        step={1}
        value={state.fps}
        onChange={e => onChange({ fps: parseInt(e.target.value, 10) || 0 })}
      />

      <PremiumSlider
        label="Quality Scale"
        displayValue={`${Math.round(state.qualityScale * 100)}%`}
        min={0.25}
        max={1}
        step={0.05}
        value={state.qualityScale}
        onChange={e => onChange({ qualityScale: parseFloat(e.target.value) })}
      />
    </>
  )
}

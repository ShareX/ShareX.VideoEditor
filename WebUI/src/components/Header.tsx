import { useState } from 'react'
import { Film, Download, AlertTriangle, Package } from 'lucide-react'
import type { RuntimeDiagnosticsSnapshot } from '../types/bridge'
import RuntimeDiagnosticsModal from './RuntimeDiagnosticsModal'
import { GlassPill, PremiumButton, Separator } from './ui'

interface HeaderProps {
  videoPath: string
  isExporting: boolean
  exportProgress: number
  exportStatusMessage: string
  ffmpegAvailable: boolean
  ffmpegPath: string
  runtimeDiagnostics: RuntimeDiagnosticsSnapshot | null
  onExport: () => void
}

export default function Header({
  videoPath,
  isExporting,
  exportProgress,
  exportStatusMessage,
  ffmpegAvailable,
  ffmpegPath,
  runtimeDiagnostics,
  onExport,
}: HeaderProps) {
  const [isDiagnosticsOpen, setIsDiagnosticsOpen] = useState(false)
  const fileName = videoPath ? videoPath.split('/').pop()?.split('\\').pop() ?? videoPath : ''
  const diagnostics = runtimeDiagnostics ?? { packageReferences: [], loadedAssemblies: [] }

  return (
    <>
      <header className="flex items-center gap-4 h-14 px-4 bg-ve-surface/80 backdrop-blur-md border-b border-white/6 shrink-0">
        <GlassPill className="h-9 px-4 gap-3">
          <div className="w-7 h-7 rounded-lg bg-ve-accent flex items-center justify-center shadow-glow-amber-sm">
            <Film className="w-4 h-4 text-white" strokeWidth={2.5} />
          </div>
          <span className="text-xs font-semibold tracking-widest text-ve-text uppercase">
            Video Editor
          </span>
          {fileName && (
            <>
              <Separator />
              <span className="font-mono text-[11px] text-ve-secondary truncate max-w-[200px]">
                {fileName}
              </span>
            </>
          )}
        </GlassPill>

        <div className="flex items-center gap-3 flex-1 justify-center" aria-live="polite" aria-atomic="true">
          {isExporting && (
            <GlassPill className="h-8 px-4 gap-3">
              <span className="text-xs text-amber-400 font-medium">{exportStatusMessage}</span>
              <div
                className="w-28 h-1.5 bg-ve-border rounded-full overflow-hidden"
                role="progressbar"
                aria-valuenow={exportProgress}
                aria-valuemin={0}
                aria-valuemax={100}
              >
                <div
                  className="h-full bg-ve-accent rounded-full transition-all duration-300 animate-pulse-soft"
                  style={{ width: `${exportProgress}%` }}
                />
              </div>
              <span className="text-[11px] font-mono text-ve-muted tracking-tighter">
                {Math.round(exportProgress)}%
              </span>
            </GlassPill>
          )}
        </div>

        <div className="flex items-center gap-2.5">
          <div className="flex items-center gap-2 min-w-0 max-w-md">
            <GlassPill className="h-7 px-3 gap-1.5 shrink-0">
              <span className="text-[11px] text-ve-secondary font-medium">FFmpeg</span>
            </GlassPill>
            {!ffmpegAvailable && (
              <GlassPill className="h-7 px-3 gap-1.5 shrink-0">
                <AlertTriangle className="w-3.5 h-3.5 text-amber-400" />
                <span className="text-[11px] text-amber-400 font-medium">FFmpeg not found</span>
              </GlassPill>
            )}
            <input
              type="text"
              readOnly
              value={ffmpegPath || '(not set)'}
              className="min-w-[120px] max-w-[280px] h-7 px-2.5 text-[11px] font-mono text-ve-secondary bg-ve-elevated/60 rounded-lg ring-1 ring-white/6 border-0 cursor-text select-text"
              title={ffmpegPath ? 'VideoEditorOptions.FFmpegPath (read-only, select to copy)' : 'Host did not provide VideoEditorOptions.FFmpegPath.'}
              aria-label="Configured FFmpeg path"
            />
          </div>
          <PremiumButton
            onClick={() => setIsDiagnosticsOpen(true)}
            size="sm"
            variant="secondary"
            icon={<Package className="w-3.5 h-3.5" />}
            title="Show loaded assemblies and package versions"
            aria-label="Show loaded assemblies and package versions"
          >
            Assemblies
          </PremiumButton>
          <PremiumButton
            onClick={onExport}
            disabled={isExporting || !ffmpegAvailable}
            size="sm"
            variant="primary"
            icon={<Download className="w-3.5 h-3.5" />}
            title="Export video (Ctrl+E)"
            aria-label="Export video"
            aria-keyshortcuts="Control+E"
          >
            Export
          </PremiumButton>
        </div>
      </header>

      {isDiagnosticsOpen && (
        <RuntimeDiagnosticsModal
          packageReferences={diagnostics.packageReferences}
          loadedAssemblies={diagnostics.loadedAssemblies}
          onClose={() => setIsDiagnosticsOpen(false)}
        />
      )}
    </>
  )
}

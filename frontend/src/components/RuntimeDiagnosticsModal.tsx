import { Package, X } from 'lucide-react'
import type { PackageReferenceInfo } from '../types/bridge'
import { PremiumIconButton } from './ui'

interface RuntimeDiagnosticsModalProps {
  packageReferences: PackageReferenceInfo[]
  onClose: () => void
}

export default function RuntimeDiagnosticsModal({
  packageReferences,
  onClose,
}: RuntimeDiagnosticsModalProps) {
  return (
    <div className="absolute inset-0 bg-ve-base/80 backdrop-blur-md flex items-center justify-center z-50 p-6">
      <div
        className="
          animate-scale-in ve-glass-heavy rounded-3xl ring-1 ring-white/8 shadow-glass-lg
          w-[min(1040px,calc(100vw-48px))] max-h-[min(82vh,760px)] overflow-hidden
          flex flex-col
        "
        role="dialog"
        aria-modal="true"
        aria-label="Loaded assemblies and package versions"
      >
        <div className="flex items-center justify-between px-6 py-5 border-b border-white/6">
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 rounded-2xl bg-ve-accent/15 ring-1 ring-amber-400/30 shadow-glow-amber-sm flex items-center justify-center">
              <Package className="w-5 h-5 text-amber-400" />
            </div>
            <div className="flex flex-col">
              <span className="text-sm font-semibold text-ve-text">Loaded Assemblies</span>
              <span className="text-xs text-ve-secondary">
                Direct package references used at runtime.
              </span>
            </div>
          </div>
          <PremiumIconButton
            onClick={onClose}
            size="sm"
            variant="ghost"
            aria-label="Close loaded assemblies dialog"
          >
            <X className="w-4 h-4" />
          </PremiumIconButton>
        </div>

        <div className="flex-1 overflow-auto px-6 py-5 space-y-6">
          <section className="space-y-3">
            <div className="flex items-center justify-between">
              <h2 className="text-sm font-semibold text-ve-text">Package References</h2>
              <span className="text-[11px] uppercase tracking-[0.22em] text-ve-muted">
                {packageReferences.length} total
              </span>
            </div>

            <div className="grid gap-2">
              {packageReferences.length > 0 ? (
                packageReferences.map(packageReference => (
                  <div
                    key={packageReference.name}
                    className="flex items-center justify-between gap-4 rounded-2xl bg-ve-elevated/55 ring-1 ring-white/6 px-4 py-3"
                  >
                    <span className="text-sm font-medium text-ve-text">{packageReference.name}</span>
                    <span className="font-mono text-xs text-amber-400">{packageReference.version}</span>
                  </div>
                ))
              ) : (
                <EmptyDiagnosticsState message="No package reference diagnostics were provided by the host." />
              )}
            </div>
          </section>
        </div>
      </div>
    </div>
  )
}

function EmptyDiagnosticsState({ message }: { message: string }) {
  return (
    <div className="rounded-2xl bg-ve-elevated/40 ring-1 ring-white/6 px-4 py-4 text-sm text-ve-muted">
      {message}
    </div>
  )
}

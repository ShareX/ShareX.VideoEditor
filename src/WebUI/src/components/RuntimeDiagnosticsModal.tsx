import { CheckCircle2, CircleOff, Package, X } from 'lucide-react'
import type { LoadedAssemblyInfo, PackageReferenceInfo } from '../types/bridge'
import { PremiumIconButton } from './ui'

interface RuntimeDiagnosticsModalProps {
  packageReferences: PackageReferenceInfo[]
  loadedAssemblies: LoadedAssemblyInfo[]
  onClose: () => void
}

export default function RuntimeDiagnosticsModal({
  packageReferences,
  loadedAssemblies,
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
                Direct package references and their runtime-loaded assembly details.
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
              {packageReferences.map(packageReference => (
                <div
                  key={packageReference.name}
                  className="flex items-center justify-between gap-4 rounded-2xl bg-ve-elevated/55 ring-1 ring-white/6 px-4 py-3"
                >
                  <span className="text-sm font-medium text-ve-text">{packageReference.name}</span>
                  <span className="font-mono text-xs text-amber-400">{packageReference.version}</span>
                </div>
              ))}
            </div>
          </section>

          <section className="space-y-3">
            <div className="flex items-center justify-between">
              <h2 className="text-sm font-semibold text-ve-text">Loaded Assembly Details</h2>
              <span className="text-[11px] uppercase tracking-[0.22em] text-ve-muted">
                {loadedAssemblies.length} tracked
              </span>
            </div>

            <div className="overflow-hidden rounded-3xl ring-1 ring-white/6 bg-ve-surface/75">
              <table className="w-full border-collapse">
                <thead className="bg-ve-elevated/70">
                  <tr className="text-left">
                    <th className="px-4 py-3 text-[11px] font-semibold uppercase tracking-[0.2em] text-ve-muted">Assembly</th>
                    <th className="px-4 py-3 text-[11px] font-semibold uppercase tracking-[0.2em] text-ve-muted">Status</th>
                    <th className="px-4 py-3 text-[11px] font-semibold uppercase tracking-[0.2em] text-ve-muted">Informational</th>
                    <th className="px-4 py-3 text-[11px] font-semibold uppercase tracking-[0.2em] text-ve-muted">Assembly</th>
                    <th className="px-4 py-3 text-[11px] font-semibold uppercase tracking-[0.2em] text-ve-muted">File</th>
                    <th className="px-4 py-3 text-[11px] font-semibold uppercase tracking-[0.2em] text-ve-muted">Location</th>
                  </tr>
                </thead>
                <tbody>
                  {loadedAssemblies.map(assembly => (
                    <AssemblyRow key={assembly.name} assembly={assembly} />
                  ))}
                </tbody>
              </table>
            </div>
          </section>
        </div>
      </div>
    </div>
  )
}

function AssemblyRow({ assembly }: { assembly: LoadedAssemblyInfo }) {
  return (
    <tr className="border-t border-white/6 align-top">
      <td className="px-4 py-3">
        <span className="font-medium text-ve-text">{assembly.name}</span>
      </td>
      <td className="px-4 py-3">
        <span
          className={`
            inline-flex items-center gap-1.5 rounded-full px-2.5 py-1 text-[11px] font-medium
            ${assembly.isLoaded
              ? 'bg-emerald-500/12 text-emerald-300 ring-1 ring-emerald-500/20'
              : 'bg-red-500/10 text-red-300 ring-1 ring-red-500/20'}
          `}
        >
          {assembly.isLoaded
            ? <CheckCircle2 className="w-3.5 h-3.5" />
            : <CircleOff className="w-3.5 h-3.5" />}
          {assembly.isLoaded ? 'Loaded' : 'Not loaded'}
        </span>
      </td>
      <td className="px-4 py-3 font-mono text-xs text-ve-secondary">{valueOrDash(assembly.informationalVersion)}</td>
      <td className="px-4 py-3 font-mono text-xs text-ve-secondary">{valueOrDash(assembly.assemblyVersion)}</td>
      <td className="px-4 py-3 font-mono text-xs text-ve-secondary">{valueOrDash(assembly.fileVersion)}</td>
      <td className="px-4 py-3">
        <span
          className="block max-w-[360px] break-all font-mono text-[11px] leading-5 text-ve-muted"
          title={assembly.location || 'Assembly is not currently loaded.'}
        >
          {valueOrDash(assembly.location)}
        </span>
      </td>
    </tr>
  )
}

function valueOrDash(value: string) {
  return value || '-'
}

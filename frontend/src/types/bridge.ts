// ─────────────────────────────────────────────────────────────────────────────
// C# ↔ React JSON bridge type definitions
// ─────────────────────────────────────────────────────────────────────────────

// ── Messages received FROM C# ─────────────────────────────────────────────────

export interface WatermarkConfig {
  enabled: boolean
  text: string
  imagePath: string
  opacity: number
  positionX: number
  positionY: number
  fontSize: number
  fontColor: string
}

export interface PackageReferenceInfo {
  name: string
  version: string
}

export interface LoadedAssemblyInfo {
  name: string
  isLoaded: boolean
  assemblyVersion: string
  informationalVersion: string
  fileVersion: string
  location: string
}

export interface RuntimeDiagnosticsSnapshot {
  packageReferences: PackageReferenceInfo[]
  loadedAssemblies: LoadedAssemblyInfo[]
}

export interface ConfigMessage {
  type: 'config'
  videoUrl: string
  theme: 'Dark' | 'Light' | 'System'
  culture: string
  ffmpegAvailable: boolean
  /** Path supplied by VideoEditorOptions.FFmpegPath (empty if not set). */
  ffmpegPath?: string
  ffprobeAvailable?: boolean
  /** Path supplied/resolved by VideoEditorOptions.FFprobePath (empty if not set). */
  ffprobePath?: string
  runtimeDiagnostics?: RuntimeDiagnosticsSnapshot | null
  watermark?: WatermarkConfig | null
}

export interface ThumbnailsMessage {
  type: 'thumbnails'
  /** Array of data:image/jpeg;base64,… URIs */
  frames: string[]
}

export interface ExportProgressMessage {
  type: 'exportProgress'
  percent: number
  message: string
}

export interface ExportCompleteMessage {
  type: 'exportComplete'
  outputPath: string
}

export interface ExportCancelledMessage {
  type: 'exportCancelled'
}

export interface ExportErrorMessage {
  type: 'exportError'
  message: string
}

export type InboundMessage =
  | ConfigMessage
  | ThumbnailsMessage
  | ExportProgressMessage
  | ExportCompleteMessage
  | ExportCancelledMessage
  | ExportErrorMessage

// ── Messages sent TO C# ───────────────────────────────────────────────────────

export interface ReadyMessage {
  type: 'ready'
}

export interface RequestExportMessage {
  type: 'requestExport'
  isTrimActive: boolean
  trimStart: number
  trimEnd: number
  isCropActive: boolean
  cropX: number
  cropY: number
  cropWidth: number
  cropHeight: number
  outputFormat: OutputFormat
  fps: number
  qualityScale: number
  watermarkEnabled: boolean
  watermarkText: string
}

export interface CancelExportMessage {
  type: 'cancelExport'
}

export type OutboundMessage = ReadyMessage | RequestExportMessage | CancelExportMessage

// ── Domain types ──────────────────────────────────────────────────────────────

export type OutputFormat = 'MP4' | 'WebM' | 'GIF' | 'WebP'
export type ActivePanel = 'trim' | 'crop' | 'watermark' | 'export'

export interface EditorState {
  videoUrl: string
  ffmpegAvailable: boolean
  ffmpegPath: string
  ffprobeAvailable: boolean
  ffprobePath: string
  runtimeDiagnostics: RuntimeDiagnosticsSnapshot | null
  watermarkConfig: WatermarkConfig | null
  theme: ConfigMessage['theme']
  // Thumbnails
  thumbnails: string[]
  // Playback
  duration: number      // seconds
  position: number      // seconds
  isPlaying: boolean
  volume: number
  // Trim
  isTrimActive: boolean
  trimStart: number     // seconds
  trimEnd: number       // seconds
  // Crop
  isCropActive: boolean
  isCropMode: boolean
  cropX: number
  cropY: number
  cropWidth: number
  cropHeight: number
  // Export settings
  outputFormat: OutputFormat
  fps: number
  qualityScale: number
  // Watermark
  watermarkEnabled: boolean
  watermarkText: string
  // Export state
  isExporting: boolean
  exportProgress: number
  exportStatusMessage: string
  // UI
  activePanel: ActivePanel
}

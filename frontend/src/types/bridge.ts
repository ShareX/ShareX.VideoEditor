// ─────────────────────────────────────────────────────────────────────────────
// C# ↔ React JSON bridge type definitions
// ─────────────────────────────────────────────────────────────────────────────

// ── Messages received FROM C# ─────────────────────────────────────────────────

export interface WatermarkConfig {
  enabled: boolean
  text: string
  imagePath: string
  imageUrl?: string
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
  protocolVersion: 2
  videoUrl: string
  theme: 'Dark' | 'Light' | 'System'
  culture: string
  ffmpegAvailable: boolean
  /** Path supplied by VideoEditorOptions.FFmpegPath (empty if not set). */
  ffmpegPath?: string
  ffprobeAvailable?: boolean
  /** Path supplied/resolved by VideoEditorOptions.FFprobePath (empty if not set). */
  ffprobePath?: string
  availableFormats?: OutputFormat[]
  runtimeDiagnostics?: RuntimeDiagnosticsSnapshot | null
  watermark?: WatermarkConfig | null
}

export interface WatermarkImageSelectedMessage {
  type: 'watermarkImageSelected'
  path: string
  imageUrl: string
}

export interface ThumbnailBatchMessage {
  type: 'thumbnailBatch'
  requestId: string
  revision: number
  startIndex: number
  totalCount: number
  /** Progressive batch of data:image/jpeg;base64,… URIs. */
  frames: string[]
  isComplete: boolean
}

export interface ExportProgressMessage {
  type: 'exportProgress'
  requestId: string
  percent: number
  message: string
}

export interface ExportCompleteMessage {
  type: 'exportComplete'
  requestId: string
  outputPath: string
}

export interface ExportCancelledMessage {
  type: 'exportCancelled'
  requestId: string
}

export interface ExportErrorMessage {
  type: 'exportError'
  requestId: string
  message: string
}

export interface BridgeErrorMessage {
  type: 'bridgeError'
  requestId?: string
  message: string
}

export type InboundMessage =
  | ConfigMessage
  | ThumbnailBatchMessage
  | ExportProgressMessage
  | ExportCompleteMessage
  | ExportCancelledMessage
  | ExportErrorMessage
  | BridgeErrorMessage
  | WatermarkImageSelectedMessage

// ── Messages sent TO C# ───────────────────────────────────────────────────────

export interface ReadyMessage {
  type: 'ready'
  protocolVersion: 2
}

export interface RequestExportMessage {
  type: 'requestExport'
  requestId: string
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
  watermarkImagePath: string
}

export interface RequestWatermarkImageMessage {
  type: 'requestWatermarkImage'
}

export interface CancelExportMessage {
  type: 'cancelExport'
  requestId: string
}

export interface RequestThumbnailsMessage {
  type: 'requestThumbnails'
  requestId: string
  revision: number
  startTime: number
  endTime: number
  count: number
}

export type OutboundMessage =
  | ReadyMessage
  | RequestExportMessage
  | CancelExportMessage
  | RequestWatermarkImageMessage
  | RequestThumbnailsMessage

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
  thumbnails: Array<string | null>
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
  watermarkImagePath: string
  watermarkImageUrl: string
  availableFormats: OutputFormat[]
  // Export state
  isExporting: boolean
  exportProgress: number
  exportStatusMessage: string
  // UI
  activePanel: ActivePanel
}

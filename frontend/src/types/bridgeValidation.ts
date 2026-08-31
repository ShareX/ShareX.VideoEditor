import type {
  InboundMessage,
  LoadedAssemblyInfo,
  OutputFormat,
  PackageReferenceInfo,
  RuntimeDiagnosticsSnapshot,
  WatermarkConfig,
} from './bridge'

type JsonRecord = Record<string, unknown>

const OUTPUT_FORMATS: readonly OutputFormat[] = ['MP4', 'WebM', 'GIF', 'WebP']
const MAX_MESSAGE_STRING = 32_768
const MAX_FRAME_LENGTH = 4_000_000
const MAX_THUMBNAILS = 512

function isRecord(value: unknown): value is JsonRecord {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
}

function isString(value: unknown, maxLength = MAX_MESSAGE_STRING): value is string {
  return typeof value === 'string' && value.length <= maxLength
}

function isOptionalString(value: unknown, maxLength = MAX_MESSAGE_STRING): value is string | undefined {
  return value === undefined || isString(value, maxLength)
}

function isFiniteNumber(value: unknown): value is number {
  return typeof value === 'number' && Number.isFinite(value)
}

function isBoolean(value: unknown): value is boolean {
  return typeof value === 'boolean'
}

function isRequestId(value: unknown): value is string {
  return isString(value, 36)
    && /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(value)
}

function isOutputFormat(value: unknown): value is OutputFormat {
  return typeof value === 'string' && OUTPUT_FORMATS.includes(value as OutputFormat)
}

function isPackageReference(value: unknown): value is PackageReferenceInfo {
  return isRecord(value) && isString(value.name, 256) && isString(value.version, 256)
}

function isLoadedAssembly(value: unknown): value is LoadedAssemblyInfo {
  return isRecord(value)
    && isString(value.name, 256)
    && isBoolean(value.isLoaded)
    && isString(value.assemblyVersion, 256)
    && isString(value.informationalVersion, 256)
    && isString(value.fileVersion, 256)
    && isString(value.location)
}

function isRuntimeDiagnostics(value: unknown): value is RuntimeDiagnosticsSnapshot {
  return isRecord(value)
    && Array.isArray(value.packageReferences)
    && value.packageReferences.length <= 256
    && value.packageReferences.every(isPackageReference)
    && Array.isArray(value.loadedAssemblies)
    && value.loadedAssemblies.length <= 256
    && value.loadedAssemblies.every(isLoadedAssembly)
}

function isWatermark(value: unknown): value is WatermarkConfig {
  return isRecord(value)
    && isBoolean(value.enabled)
    && isString(value.text, 8_192)
    && isString(value.imagePath)
    && isOptionalString(value.imageUrl)
    && isFiniteNumber(value.opacity)
    && value.opacity >= 0
    && value.opacity <= 1
    && isFiniteNumber(value.positionX)
    && value.positionX >= 0
    && value.positionX <= 1
    && isFiniteNumber(value.positionY)
    && value.positionY >= 0
    && value.positionY <= 1
    && isFiniteNumber(value.fontSize)
    && value.fontSize > 0
    && value.fontSize <= 1_000
    && isString(value.fontColor, 64)
}

function isConfigMessage(value: JsonRecord): boolean {
  return value.protocolVersion === 2
    && isString(value.videoUrl)
    && (value.theme === 'Dark' || value.theme === 'Light' || value.theme === 'System')
    && isString(value.culture, 128)
    && isBoolean(value.ffmpegAvailable)
    && isOptionalString(value.ffmpegPath)
    && (value.ffprobeAvailable === undefined || isBoolean(value.ffprobeAvailable))
    && isOptionalString(value.ffprobePath)
    && (value.availableFormats === undefined
      || (Array.isArray(value.availableFormats)
        && value.availableFormats.length <= OUTPUT_FORMATS.length
        && value.availableFormats.every(isOutputFormat)))
    && (value.runtimeDiagnostics === undefined
      || value.runtimeDiagnostics === null
      || isRuntimeDiagnostics(value.runtimeDiagnostics))
    && (value.watermark === undefined || value.watermark === null || isWatermark(value.watermark))
}

function isThumbnailDataUri(value: unknown): value is string {
  return isString(value, MAX_FRAME_LENGTH)
    && /^data:image\/(?:jpeg|webp);base64,[A-Za-z0-9+/=]+$/.test(value)
}

function isThumbnailBatchMessage(value: JsonRecord): boolean {
  return isRequestId(value.requestId)
    && Number.isSafeInteger(value.revision)
    && (value.revision as number) >= 0
    && Number.isSafeInteger(value.startIndex)
    && (value.startIndex as number) >= 0
    && Number.isSafeInteger(value.totalCount)
    && (value.totalCount as number) > 0
    && (value.totalCount as number) <= MAX_THUMBNAILS
    && Array.isArray(value.frames)
    && value.frames.length <= MAX_THUMBNAILS
    && value.frames.every(isThumbnailDataUri)
    && (value.startIndex as number) + value.frames.length <= (value.totalCount as number)
    && isBoolean(value.isComplete)
}

/** Parse and validate an untrusted message from the native host. */
export function parseInboundMessage(raw: string): InboundMessage | null {
  let value: unknown
  try {
    value = JSON.parse(raw)
  } catch {
    return null
  }

  if (!isRecord(value) || !isString(value.type, 64)) return null

  switch (value.type) {
    case 'config':
      return isConfigMessage(value) ? value as unknown as InboundMessage : null
    case 'watermarkImageSelected':
      return isString(value.path) && isString(value.imageUrl)
        ? value as unknown as InboundMessage
        : null
    case 'thumbnailBatch':
      return isThumbnailBatchMessage(value) ? value as unknown as InboundMessage : null
    case 'exportProgress':
      return isRequestId(value.requestId)
        && isFiniteNumber(value.percent)
        && value.percent >= 0
        && value.percent <= 100
        && isString(value.message, 2_048)
        ? value as unknown as InboundMessage
        : null
    case 'exportComplete':
      return isRequestId(value.requestId) && isString(value.outputPath)
        ? value as unknown as InboundMessage
        : null
    case 'exportCancelled':
      return isRequestId(value.requestId) ? value as unknown as InboundMessage : null
    case 'exportError':
      return isRequestId(value.requestId) && isString(value.message, 8_192)
        ? value as unknown as InboundMessage
        : null
    case 'bridgeError':
      return (value.requestId === undefined || isRequestId(value.requestId)) && isString(value.message, 8_192)
        ? value as unknown as InboundMessage
        : null
    default:
      return null
  }
}

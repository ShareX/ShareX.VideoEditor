# ShareX.VideoEditor

Cross-platform video editor library for ShareX. Provides trimming, cropping, format conversion, and watermarking via a hybrid Photino + React UI and FFmpeg.

Exports are transactional: FFmpeg writes to a unique sibling staging file and the destination is replaced only after a successful encode. Cancellation and failure preserve any existing destination, and exporting over the source video is rejected.

FFmpeg and FFprobe are launched without shell command parsing through structured argument lists. Their output streams are drained concurrently, cancellation terminates the complete process tree, and export progress uses FFmpeg's machine-readable progress protocol. The native/Web UI bridge is protocol-versioned, validates messages at runtime, and correlates export and thumbnail work so stale responses cannot mutate newer operations.

## Requirements

- .NET 10
- Node.js `^20.19.0 || >=22.12.0` (required by Vite 8 for building the frontend)
- FFmpeg (and optionally FFprobe) supplied by the host application

## Build

```bash
# Build C# library and frontend (validates Node.js, then runs npm ci and npm run build in frontend)
dotnet build ShareX.VideoEditor.sln

# Build without building frontend (use when dist is pre-built)
dotnet build ShareX.VideoEditor.sln -p:BuildWebUI=false

# Run backend regression tests
dotnet test tests/ShareX.VideoEditor.Tests.csproj

# Run frontend unit and component tests
cd frontend
npm test
```

## Editing shortcuts

- `Space`: play or pause the selected clip
- `Left` / `Right`: move the playhead by 1 second
- `Shift+Left` / `Shift+Right`: move by 5 seconds
- `Alt+Left` / `Alt+Right`: fine-adjust by 0.2 seconds
- `I` / `O`: set the trim in/out point at the playhead
- `Ctrl+S` or `Ctrl+E`: export

When a trim is active, playback and seeking stay inside its in/out range. Crop state remains applied after leaving crop-edit mode until **Reset Crop** is selected.

Use **Zoom to selection** to expand the active trim range while retaining context on each side. **Show full timeline** restores the complete recording. Thumbnail requests follow the visible range and arrive progressively in fixed timeline slots.

## Layout

- **backend/** - C# class library (host API, export, thumbnails, Photino bridge)
- **frontend/** - React + TypeScript + Vite front-end; output in `frontend/dist/` is embedded in the assembly output

## Integration

Consumed as a Git submodule by [XerahS](https://github.com/ShareX/XerahS). Host applications pass `VideoEditorOptions` and `VideoEditorEvents` to `VideoEditorHost.ShowEditor` or `ShowEditorDialog`.

## License

GPL v3 - see [LICENSE](LICENSE).

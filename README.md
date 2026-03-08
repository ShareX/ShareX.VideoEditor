# ShareX.VideoEditor

Cross-platform video editor library for ShareX. Provides trimming, cropping, format conversion, and watermarking via a hybrid Photino + React UI and FFmpeg.

## Requirements

- .NET 10
- Node.js (for building the WebUI)
- FFmpeg (and optionally FFprobe) — supplied by the host application

## Build

```bash
# Build C# library and WebUI (runs npm ci + npm run build in WebUI)
dotnet build ShareX.VideoEditor.sln

# Build without building WebUI (use when dist is pre-built)
dotnet build ShareX.VideoEditor.sln -p:BuildWebUI=false
```

## Layout

- **ShareX.VideoEditor/** — C# class library (host API, export, thumbnails, Photino bridge)
- **WebUI/** — React + TypeScript + Vite front-end; output in `WebUI/dist/` is embedded in the assembly output

## Integration

Consumed as a Git submodule by [XerahS](https://github.com/ShareX/XerahS). Host applications pass `VideoEditorOptions` and `VideoEditorEvents` to `VideoEditorHost.ShowEditor` or `ShowEditorDialog`.

## License

GPL v3 — see [LICENSE](LICENSE).

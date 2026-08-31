#region License Information (GPL v3)

/*
    ShareX.VideoEditor - The UI-agnostic Video Editor library for ShareX
    Copyright (c) 2007-2026 ShareX Team
*/

#endregion License Information (GPL v3)

using Newtonsoft.Json;

namespace ShareX.VideoEditor.Hosting.Bridge;

internal sealed class ThumbnailRequestPayload
{
    [JsonProperty("requestId")]
    public string RequestId { get; set; } = string.Empty;

    [JsonProperty("revision")]
    public int Revision { get; set; }

    [JsonProperty("startTime")]
    public double StartTime { get; set; }

    [JsonProperty("endTime")]
    public double EndTime { get; set; }

    [JsonProperty("count")]
    public int Count { get; set; }
}

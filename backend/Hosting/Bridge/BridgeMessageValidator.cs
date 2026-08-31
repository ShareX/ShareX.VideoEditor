#region License Information (GPL v3)

/*
    ShareX.VideoEditor - The UI-agnostic Video Editor library for ShareX
    Copyright (c) 2007-2026 ShareX Team
*/

#endregion License Information (GPL v3)

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ShareX.VideoEditor.Hosting.Bridge;

internal sealed record BridgeMessageValidationResult(
    bool IsValid,
    string Type,
    string? RequestId,
    JObject? Message,
    string? Error);

internal static class BridgeMessageValidator
{
    private const int MaximumMessageLength = 64 * 1024;
    private const double MaximumMediaSeconds = 604_800;
    private static readonly HashSet<string> OutputFormats = new(StringComparer.OrdinalIgnoreCase)
    {
        "MP4", "WebM", "GIF", "WebP"
    };
    private static readonly HashSet<string> WatermarkExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".webp"
    };
    private static readonly HashSet<string> ExportProperties = new(StringComparer.Ordinal)
    {
        "type", "requestId", "isTrimActive", "trimStart", "trimEnd", "isCropActive",
        "cropX", "cropY", "cropWidth", "cropHeight", "outputFormat", "fps", "qualityScale",
        "watermarkEnabled", "watermarkText", "watermarkImagePath"
    };
    private static readonly HashSet<string> ThumbnailProperties = new(StringComparer.Ordinal)
    {
        "type", "requestId", "revision", "startTime", "endTime", "count"
    };

    public static BridgeMessageValidationResult Validate(string message)
    {
        if (string.IsNullOrWhiteSpace(message) || message.Length > MaximumMessageLength)
        {
            return Invalid(string.Empty, null, "Bridge message is empty or exceeds 64 KiB.");
        }

        JObject obj;
        try
        {
            using var stringReader = new StringReader(message);
            using var jsonReader = new JsonTextReader(stringReader)
            {
                DateParseHandling = DateParseHandling.None,
                MaxDepth = 16
            };
            JToken token = JToken.Load(jsonReader, new JsonLoadSettings
            {
                DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error
            });
            if (token is not JObject parsedObject || jsonReader.Read())
            {
                return Invalid(string.Empty, null, "Bridge message must contain exactly one JSON object.");
            }

            obj = parsedObject;
        }
        catch
        {
            return Invalid(string.Empty, null, "Bridge message is not valid JSON.");
        }

        string type = obj["type"] is JValue { Type: JTokenType.String } typeValue
            ? typeValue.Value<string>()?.Trim() ?? string.Empty
            : string.Empty;
        string? requestId = obj["requestId"] is JValue { Type: JTokenType.String } requestIdValue
            ? requestIdValue.Value<string>()
            : null;
        switch (type)
        {
            case "ready":
                return HasOnlyProperties(obj, "type", "protocolVersion") &&
                       IsInteger(obj["protocolVersion"], 2, 2)
                    ? Valid(type, null, obj)
                    : Invalid(type, null, "ready requires protocolVersion 2.");

            case "requestExport":
                return ValidateExport(obj, requestId);

            case "cancelExport":
                return HasOnlyProperties(obj, "type", "requestId")
                    ? ValidateRequestId(type, requestId, obj)
                    : Invalid(type, requestId, "cancelExport contains unsupported fields.");

            case "requestThumbnails":
                return ValidateThumbnails(obj, requestId);

            case "requestWatermarkImage":
                if (!ContainsOnlyProperties(obj, "type", "requestId"))
                {
                    return Invalid(type, requestId, "requestWatermarkImage contains unsupported fields.");
                }

                if (requestId != null && !IsRequestId(requestId))
                {
                    return Invalid(type, null, "requestId must be a UUID.");
                }

                return Valid(type, requestId, obj);

            default:
                return Invalid(type, IsRequestId(requestId) ? requestId : null, "Unknown bridge message type.");
        }
    }

    private static BridgeMessageValidationResult ValidateExport(JObject obj, string? requestId)
    {
        if (!HasOnlyProperties(obj, ExportProperties))
        {
            return Invalid("requestExport", requestId, "Export request contains missing or unsupported fields.");
        }

        BridgeMessageValidationResult idResult = ValidateRequestId("requestExport", requestId, obj);
        if (!idResult.IsValid)
        {
            return idResult;
        }

        if (!IsBoolean(obj["isTrimActive"]) || !IsNumber(obj["trimStart"]) || !IsNumber(obj["trimEnd"]) ||
            !IsBoolean(obj["isCropActive"]) || !IsInteger(obj["cropX"], 0, 32_768) ||
            !IsInteger(obj["cropY"], 0, 32_768) || !IsInteger(obj["cropWidth"], 0, 32_768) ||
            !IsInteger(obj["cropHeight"], 0, 32_768) || !IsString(obj["outputFormat"]) ||
            !IsNumber(obj["fps"]) || !IsNumber(obj["qualityScale"]) ||
            !IsBoolean(obj["watermarkEnabled"]) || !IsString(obj["watermarkText"]) ||
            !IsString(obj["watermarkImagePath"]))
        {
            return Invalid("requestExport", requestId, "Export payload has missing or invalid field types.");
        }

        ExportPayload? payload;
        try
        {
            payload = obj.ToObject<ExportPayload>();
        }
        catch
        {
            return Invalid("requestExport", requestId, "Export payload has invalid field types.");
        }

        if (payload == null || !OutputFormats.Contains(payload.OutputFormat))
        {
            return Invalid("requestExport", requestId, "outputFormat must be MP4, WebM, GIF, or WebP.");
        }

        if (!InRange(payload.Fps, 0, 240))
        {
            return Invalid("requestExport", requestId, "fps must be finite and between 0 and 240.");
        }

        if (!InRange(payload.QualityScale, 0.1, 4))
        {
            return Invalid("requestExport", requestId, "qualityScale must be finite and between 0.1 and 4.");
        }

        if (!InRange(payload.TrimStart, 0, MaximumMediaSeconds) ||
            !InRange(payload.TrimEnd, 0, MaximumMediaSeconds) ||
            (payload.IsTrimActive && payload.TrimEnd - payload.TrimStart < 0.1))
        {
            return Invalid("requestExport", requestId, "Active trim range must be finite, bounded, and at least 0.1 seconds long.");
        }

        if (payload.IsCropActive &&
            (payload.CropX < 0 || payload.CropY < 0 || payload.CropWidth < 2 || payload.CropHeight < 2))
        {
            return Invalid("requestExport", requestId, "Active crop must have non-negative coordinates and dimensions of at least 2 pixels.");
        }

        if ((payload.WatermarkText?.Length ?? 0) > 1024 ||
            (payload.WatermarkText?.Any(static character => char.IsControl(character) && character is not '\r' and not '\n' and not '\t') ?? false) ||
            (payload.WatermarkImagePath?.Length ?? 0) > 32_767)
        {
            return Invalid("requestExport", requestId, "Watermark values exceed their maximum length.");
        }

        if (!string.IsNullOrWhiteSpace(payload.WatermarkImagePath) &&
            !WatermarkExtensions.Contains(Path.GetExtension(payload.WatermarkImagePath)))
        {
            return Invalid("requestExport", requestId, "Watermark image must be PNG, JPEG, or WebP.");
        }

        return Valid("requestExport", requestId, obj);
    }

    private static BridgeMessageValidationResult ValidateThumbnails(JObject obj, string? requestId)
    {
        if (!HasOnlyProperties(obj, ThumbnailProperties) ||
            !IsInteger(obj["revision"], 1, int.MaxValue) ||
            !IsNumber(obj["startTime"]) || !IsNumber(obj["endTime"]) ||
            !IsInteger(obj["count"], 1, 120))
        {
            return Invalid("requestThumbnails", requestId, "Thumbnail payload has missing or invalid field types.");
        }

        BridgeMessageValidationResult idResult = ValidateRequestId("requestThumbnails", requestId, obj);
        if (!idResult.IsValid)
        {
            return idResult;
        }

        ThumbnailRequestPayload? payload;
        try
        {
            payload = obj.ToObject<ThumbnailRequestPayload>();
        }
        catch
        {
            return Invalid("requestThumbnails", requestId, "Thumbnail payload has invalid field types.");
        }

        if (payload == null || payload.Revision <= 0 || payload.Count is < 1 or > 120 ||
            !InRange(payload.StartTime, 0, MaximumMediaSeconds) ||
            !InRange(payload.EndTime, 0, MaximumMediaSeconds) ||
            payload.EndTime <= payload.StartTime)
        {
            return Invalid(
                "requestThumbnails",
                requestId,
                "Thumbnail request requires revision > 0, count 1..120, and a finite increasing time range.");
        }

        return Valid("requestThumbnails", requestId, obj);
    }

    private static BridgeMessageValidationResult ValidateRequestId(string type, string? requestId, JObject obj)
    {
        return IsRequestId(requestId)
            ? Valid(type, requestId, obj)
            : Invalid(type, null, "requestId must be a UUID.");
    }

    private static bool IsRequestId(string? requestId) =>
        requestId is { Length: 36 } && Guid.TryParseExact(requestId, "D", out _);

    private static bool HasOnlyProperties(JObject obj, params string[] propertyNames) =>
        HasOnlyProperties(obj, new HashSet<string>(propertyNames, StringComparer.Ordinal));

    private static bool HasOnlyProperties(JObject obj, IReadOnlySet<string> propertyNames) =>
        obj.Count == propertyNames.Count && obj.Properties().All(property => propertyNames.Contains(property.Name));

    private static bool ContainsOnlyProperties(JObject obj, params string[] propertyNames)
    {
        var allowed = new HashSet<string>(propertyNames, StringComparer.Ordinal);
        return obj.Properties().All(property => allowed.Contains(property.Name));
    }

    private static bool IsString(JToken? token) => token is JValue { Type: JTokenType.String };

    private static bool IsBoolean(JToken? token) => token is JValue { Type: JTokenType.Boolean };

    private static bool IsNumber(JToken? token) =>
        token is JValue { Type: JTokenType.Integer or JTokenType.Float } &&
        double.IsFinite(token.Value<double>());

    private static bool IsInteger(JToken? token, int minimum, int maximum) =>
        token is JValue { Type: JTokenType.Integer } &&
        token.Value<long>() >= minimum && token.Value<long>() <= maximum;

    private static bool InRange(double value, double minimum, double maximum) =>
        double.IsFinite(value) && value >= minimum && value <= maximum;

    private static BridgeMessageValidationResult Valid(string type, string? requestId, JObject obj) =>
        new(true, type, requestId, obj, null);

    private static BridgeMessageValidationResult Invalid(string type, string? requestId, string error) =>
        new(false, type, requestId, null, error);
}

using System.Text.Json.Serialization;

namespace Lyrava.Sources.YouTube;

internal sealed record ClientContext(string ClientName, string ClientVersion, string Hl = "en", string Gl = "US");
internal sealed record RequestContext(ClientContext Client);
internal sealed record SearchRequest(RequestContext Context, string Query);
internal sealed record PlayerRequest(RequestContext Context, string VideoId, bool ContentCheckOk = false, bool RacyCheckOk = false);
internal sealed record VisionOsClientContext(string ClientName, string ClientVersion, string DeviceMake, string DeviceModel, string OsName, string OsVersion, string VisitorData, string Hl = "en", string Gl = "US", int UtcOffsetMinutes = 0);
internal sealed record VisionOsRequest(string VideoId, bool ContentCheckOk, bool RacyCheckOk, VisionOsRequestContext Context);
internal sealed record VisionOsRequestContext(VisionOsClientContext Client);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(SearchRequest))]
[JsonSerializable(typeof(PlayerRequest))]
[JsonSerializable(typeof(VisionOsRequest))]
internal partial class InnerTubeJsonContext : JsonSerializerContext;

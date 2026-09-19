using System.Text.Json.Serialization;
using GeekFlashToolX.Core.Models;

namespace GeekFlashToolX.Services.Serialization;

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(AppSettings))]
[JsonSerializable(typeof(WorkLogInfo))]
[JsonSerializable(typeof(UpdateManifest))]
[JsonSerializable(typeof(Dictionary<string, string>))]
internal partial class AppJsonContext : JsonSerializerContext;

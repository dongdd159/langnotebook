using System.Text.Json.Serialization;
using LangNotebook.Dto;

namespace LangNotebook.Helper;

[JsonSourceGenerationOptions(WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.Never)]
[JsonSerializable(typeof(BackupPayload))]
internal partial class BackupJsonContext : JsonSerializerContext
{
}
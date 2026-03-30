using System.Text.Json.Serialization;

namespace RomCleanup.Api;

[JsonSerializable(typeof(ApiReviewApprovalRequest))]
internal sealed partial class ApiJsonSerializerContext : JsonSerializerContext
{
}

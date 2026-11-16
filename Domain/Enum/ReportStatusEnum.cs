using System.Text.Json.Serialization;

namespace Domain.Enum
{
    [JsonConverter(typeof(JsonStringEnumConverter<ReportStatusEnum>))]
    public enum ReportStatusEnum
    {
        Pending,
        Approved,
        Rejected
    }
}

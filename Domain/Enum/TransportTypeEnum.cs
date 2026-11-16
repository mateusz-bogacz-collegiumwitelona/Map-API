using System.Text.Json.Serialization;

namespace Domain.Enum
{
    [JsonConverter(typeof(JsonStringEnumConverter<TransportTypeEnum>))]
    public enum TransportTypeEnum
    {
        Car,
        Plane,
        Train,
        Other
    }
}

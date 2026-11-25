using System.Text.Json;
using System.Text.Json.Serialization;
namespace Vairas.SchoolManagement.Business;

public static class CoordinationWire
{
    public static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web){Converters={new JsonStringEnumConverter()}};
    public static string Save<T>(T value)=>JsonSerializer.Serialize(value,Json);
    public static T Read<T>(string value)=>JsonSerializer.Deserialize<T>(value,Json)??throw SchoolErrors.Dependency();
}

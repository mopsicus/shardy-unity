using System.Text;
using NiceJson;
using Shardy;

/// <summary>
/// Simple example serializer via NiceJson
/// </summary>
class MyJSON : ISerializer {

    /// <summary>
    /// Decode received data to PayloadData
    /// </summary>
    public PayloadData Decode(byte[] body) {
        var data = (JsonObject)JsonNode.ParseJsonString(Encoding.UTF8.GetString(body));
        return new PayloadData((PayloadType)(int)data["type"], data["name"], data["id"], Encoding.UTF8.GetBytes(data["data"]), data["error"]);
    }

    /// <summary>
    /// Encode to byte array for transporting
    /// </summary>
    public byte[] Encode(PayloadData payload) {
        var data = new JsonObject();
        data["type"] = (int)payload.Type;
        data["name"] = payload.Name;
        data["id"] = payload.Id;
        data["data"] = (payload.Data != null) ? Encoding.UTF8.GetString(payload.Data) : string.Empty;
        data["error"] = payload.Error;
        return Encoding.UTF8.GetBytes(data.ToJsonString());
    }
}
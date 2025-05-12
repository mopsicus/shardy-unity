using UnityEngine;
using Shardy;

[DefaultExecutionOrder(-1)]
public class Connector : MonoBehaviour {

    /// <summary>
    /// Client instance
    /// </summary>
    static Client _client = null;

    /// <summary>
    /// Init
    /// 
    /// This demo uses TCP transport for connecting to test game server
    /// And uses test validator and serializer
    /// Make sure to use your own validator and serializer on backend and client
    /// </summary>
    void Awake() {
        if (_client == null) {
            _client = new Client(new TestHandshake(), new TestSerializer(), new ClientOptions(TransportType.WebSocket));
            DontDestroyOnLoad(gameObject);
        }
    }

    /// <summary>
    /// Get client instance for external use
    /// </summary>
    public static Client Use() {
        return _client;
    }
}

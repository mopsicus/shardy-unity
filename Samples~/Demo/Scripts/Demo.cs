using UnityEngine;
using Shardy;
using System.Text;
using TMPro;
using UnityEngine.UI;

public class Demo : MonoBehaviour {

    /// <summary>
    /// Link to log text
    /// </summary>
    [SerializeField]
    TextMeshProUGUI LogText = null;

    /// <summary>
    /// Link to status text
    /// </summary>
    [SerializeField]
    TextMeshProUGUI StatusText = null;

    /// <summary>
    /// Input field for host
    /// </summary>
    [SerializeField]
    TMP_InputField HostInput = null;

    /// <summary>
    /// Input field for port
    /// </summary>
    [SerializeField]
    TMP_InputField PortInput = null;

    /// <summary>
    /// Input field for data
    /// </summary>
    [SerializeField]
    TMP_InputField DataInput = null;

    /// <summary>
    /// Scroll for logs
    /// </summary>
    [SerializeField]
    ScrollRect LogScroll = null;

    /// <summary>
    /// Link to connect button
    /// </summary>
    [SerializeField]
    Button ConnectButton = null;

    /// <summary>
    /// Array with buttons
    /// </summary>
    [SerializeField]
    Button[] Buttons = null;

    /// <summary>
    /// Client instance
    /// </summary>
    Client _client = null;

    /// <summary>
    /// Cached data
    /// </summary>
    byte[] _data = null;

    /// <summary>
    /// Init
    /// 
    /// This demo uses TCP transport for connecting to test service
    /// If you want to test client on your own backend with Websockets, change it in ClientOptions
    /// </summary>
    void Awake() {
        LogScroll.verticalNormalizedPosition = 1f;
        SwitchControls(false);
        _client = new Client(new MyHandshake(), new MyJSON(), new ClientOptions());
        _client.OnConnect += OnConnect;
        _client.OnDisconnect += OnDisconnect;
        _client.OnReady += OnReady;
    }

    /// <summary>
    /// Destroy client
    /// </summary>
    void OnDestroy() {
        _client.Destroy();
    }

    /// <summary>
    /// Callback on ready to work
    /// </summary>
    void OnReady() {
        StatusText.text = "Ready";
        LogText.text += $"< ready\n";
    }

    /// <summary>
    /// Callback on disconnect with reason
    /// </summary>
    void OnDisconnect(DisconnectReason reason) {
        LogText.text += $"< disconnected\n";
        StatusText.text = $"Disconnected: {reason}";
        Scroll();
        SwitchControls(false);
    }

    /// <summary>
    /// Callback on connect with status
    /// </summary>
    void OnConnect(bool status) {
        LogText.text += status ? $"< connected\n" : $"< failed\n";
        StatusText.text = status ? "Connected" : "Disconnected";
        SwitchControls(status);
        if (status) {
            _client.Handshake();
        }
    }

    /// <summary>
    /// On/off controls
    /// </summary>
    void SwitchControls(bool value) {
        ConnectButton.interactable = !value;
        foreach (var item in Buttons) {
            item.interactable = value;
        }
        DataInput.interactable = value;
    }

    /// <summary>
    /// Connect to server
    /// </summary>
    public void Connect() {
        var host = HostInput.text.Trim();
        var port = int.Parse(PortInput.text.Trim());
        LogText.text += $"> connect to: {host}:{port}\n";
        _client.Connect(host, port);
    }

    /// <summary>
    /// Disconnect
    /// </summary>
    public void Disconnect() {
        LogText.text += $"> disconnect\n";
        _client.Disconnect();
        Scroll();
    }

    /// <summary>
    /// Test request
    /// </summary>
    public void Request() {
        PrepareData();
        var request = "status";
        LogText.text += $"> test request: {request}\n";
        _client.Request(request, _data, (response) => {
            LogText.text += $"< request data: {Utils.DataToString(response.Data)}\n\n";
            Scroll();
        });
        Scroll();
    }

    /// <summary>
    /// Test command
    /// </summary>
    public void Command() {
        PrepareData();
        var command = "notify";
        LogText.text += $"> test command: {command}\n";
        _client.Command(command, _data);
        Scroll();
    }

    /// <summary>
    /// Test request with error response
    /// </summary>
    public void Error() {
        PrepareData();
        var request = "fail";
        LogText.text += $"> test error: {request}\n";
        _client.Request(request, _data, (response) => {
            LogText.text += $"< error data: {Utils.DataToString(response.Data)}\n\n";
            Scroll();
        });
        Scroll();
    }

    /// <summary>
    /// Subscribe on command
    /// </summary>
    public void Subscribe() {
        PrepareData();
        var command = "timer";
        LogText.text += $"> subscribe on command: {command}\n";
        _client.Command(command, Encoding.UTF8.GetBytes("yes"));
        _client.On(command, OnCommand);
        Scroll();
    }

    /// <summary>
    /// Unsubscribe from command
    /// </summary>
    public void Unsubscribe() {
        PrepareData();
        var command = "timer";
        LogText.text += $"> unsubscribe from command: {command}\n";
        _client.Command(command, Encoding.UTF8.GetBytes("no"));
        _client.Off(command, OnCommand);
        Scroll();
    }

    /// <summary>
    /// Add callback for request from server
    /// Need respond or will be timeout
    /// 
    /// Send command to initiate this example
    /// </summary>
    public void SubscribeRequest() {
        var request = "request";
        LogText.text += $"> subscribe on request: {request}\n";
        _client.OnRequest(request, (payload) => {
            // make response on this request id
            //
            // if comment this, you will give timeout on your backend
            _client.Response(payload, Encoding.UTF8.GetBytes("some_data_from_client"));
        });
        _client.Command(request);
    }

    /// <summary>
    /// Unsubscribe from server request 
    /// 
    /// Send command to initiate example and check that callback above doesn't invoke
    /// </summary>
    public void UnsubscribeRequest() {
        var request = "request";
        LogText.text += $"> unsubscribe from request: {request}\n";
        _client.OffRequest(request);
        _client.Command(request);
    }

    /// <summary>
    /// Callback on subscribed command
    /// </summary>
    void OnCommand(PayloadData payload) {
        LogText.text += $"< subscribe data: {Utils.DataToString(payload.Data)}\n\n";
        Scroll();
    }

    /// <summary>
    /// Prepare data to transport
    /// </summary>
    void PrepareData() {
        _data = !string.IsNullOrEmpty(DataInput.text) ? Encoding.UTF8.GetBytes(DataInput.text) : null;
    }

    /// <summary>
    /// Scroll to bottom with some step
    /// </summary>
    void Scroll() {
        LogScroll.verticalNormalizedPosition -= 0.035f;
    }
}

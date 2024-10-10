//
// This is simple game client for Tic-Tac-Toe game
// You can get game server source code here: https://github.com/mopsicus/shardy-tictactoe
//

using UnityEngine;
using Shardy;
using TMPro;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class MenuController : MonoBehaviour {

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
    /// Link to status text
    /// </summary>
    [SerializeField]
    TextMeshProUGUI StatusText = null;

    /// <summary>
    /// Search panel
    /// </summary>
    [SerializeField]
    GameObject SearchPanel = null;

    /// <summary>
    /// Play button
    /// </summary>
    [SerializeField]
    Button PlayButton = null;

    /// <summary>
    /// Init
    /// </summary>
    void Awake() {
        Connector.Use().OnConnect += OnConnect;
        Connector.Use().OnDisconnect += OnDisconnect;
        Connector.Use().OnReady += OnReady;
        Connector.Use().On(Consts.PLAY, (data) => {
            SceneManager.LoadScene(Consts.GAME_SCENE);
        });
        if (Connector.Use().IsConnected) {
            PlayButton.interactable = true;
            StatusText.text = "Ready for play";
        }
    }

    /// <summary>
    /// Unsubscribe on exit
    /// </summary>
    void OnDisable() {
        Connector.Use().OnConnect -= OnConnect;
        Connector.Use().OnDisconnect -= OnDisconnect;
        Connector.Use().OnReady -= OnReady;
        Connector.Use().Off(Consts.PLAY);
    }

    /// <summary>
    /// Callback on ready to work
    /// </summary>
    void OnReady() {
        StatusText.text = "Ready for play";
    }

    /// <summary>
    /// Callback on disconnect with reason
    /// </summary>
    void OnDisconnect(DisconnectReason reason) {
        PlayButton.interactable = false;
        StatusText.text = $"Disconnected: {reason}";
    }

    /// <summary>
    /// Callback on connect with status
    /// </summary>
    void OnConnect(bool status) {
        PlayButton.interactable = status;
        StatusText.text = status ? "Connected" : "Disconnected";
        if (status) {
            Connector.Use().Handshake();
        }
    }

    /// <summary>
    /// Connect to server
    /// </summary>
    public void Connect() {
        var host = HostInput.text.Trim();
        var port = int.Parse(PortInput.text.Trim());
        Connector.Use().Connect(host, port);
    }

    /// <summary>
    /// Disconnect
    /// </summary>
    public void Disconnect() {
        Connector.Use().Disconnect();
    }

    /// <summary>
    /// Start play
    /// </summary>
    public void Play() {
        SearchPanel.SetActive(true);
        Connector.Use().Request(Consts.SEARCH_START, (data) => {
            if (!string.IsNullOrEmpty(data.Error)) {
                SearchPanel.SetActive(false);
                StatusText.text = data.Error;
            }
        });
    }

    /// <summary>
    /// Stop opponent search
    /// </summary>
    public void Stop() {
        Connector.Use().Request(Consts.SEARCH_STOP, (data) => {
            if (!string.IsNullOrEmpty(data.Error)) {
                StatusText.text = data.Error;
            }
            SearchPanel.SetActive(false);
        });
    }
}

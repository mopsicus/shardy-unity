using System;
using System.Text;
using NiceJson;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Type of player' game item
/// </summary>
public enum ItemType {
    Cross,
    Circle,
    None
}

/// <summary>
/// Game info structure 
/// </summary>
public struct GameInfo {

    /// <summary>
    /// Current game id
    /// </summary>
    public string GameId;

    /// <summary>
    /// Current player id
    /// </summary>
    public int PlayerId;

    /// <summary>
    /// Current turn player id
    /// </summary>
    public int TurnPlayerId;

    /// <summary>
    /// Score data
    /// </summary> 
    public int[] Score;
}

public class GameController : MonoBehaviour {

    /// <summary>
    /// Sprites count in row
    /// </summary>
    const int ROW_MAX_COUNT = 3;

    /// <summary>
    /// Container with game sprites
    /// </summary>
    [SerializeField]
    Transform PlayField = null;

    /// <summary>
    /// Link to cross sprite
    /// </summary>
    [SerializeField]
    Sprite CrossIcon = null;

    /// <summary>
    /// Link to circle sprite
    /// </summary>
    [SerializeField]
    Sprite CircleIcon = null;

    /// <summary>
    /// Link to cross color picker
    /// </summary>
    [SerializeField]
    Color CrossColor = Color.red;

    /// <summary>
    /// Link to circle color picker
    /// </summary>
    [SerializeField]
    Color CircleColor = Color.green;

    /// <summary>
    /// Link to score text
    /// </summary>
    [SerializeField]
    TextMeshProUGUI ScoreText = null;

    /// <summary>
    /// Link to status text
    /// </summary>
    [SerializeField]
    TextMeshProUGUI StatusText = null;

    /// <summary>
    /// Sprites in grid
    /// </summary>
    readonly Image[,] _field = new Image[ROW_MAX_COUNT, ROW_MAX_COUNT];

    /// <summary>
    /// Current game info
    /// </summary>
    GameInfo _info = new GameInfo();

    /// <summary>
    /// Init
    /// </summary>
    void Awake() {
        var images = PlayField.GetComponentsInChildren<Image>();
        var i = 0;
        for (var x = 0; x < ROW_MAX_COUNT; x++) {
            for (var y = 0; y < ROW_MAX_COUNT; y++) {
                _field[x, y] = images[i];
                var button = images[i].GetComponent<ItemButton>();
                button.Init(this, x, y);
                i++;
            }
        }
        Clear();
        SetStatus("Wait, please...");
        Connector.Use().On(Consts.ROUND, (response) => {
            var json = Encoding.UTF8.GetString(response.Data);
            try {
                var data = (JsonObject)JsonNode.ParseJsonString(json);
                _info.GameId = data["g"];
                _info.PlayerId = data["p"];
                _info.TurnPlayerId = data["t"];
                _info.Score = new int[] { data["s"][0], data["s"][1] };
            } catch (Exception e) {
                Debug.LogException(e);
            }
            Clear();
            SetScore();
            var status = _info.PlayerId != _info.TurnPlayerId ? "Wait for the opponent's step" : "Make your step";
            SetStatus(status);
        });
        Connector.Use().On(Consts.TURN, (response) => {
            var json = Encoding.UTF8.GetString(response.Data);
            try {
                var data = (JsonObject)JsonNode.ParseJsonString(json);
                SetItem(data["x"], data["y"], (ItemType)(int)data["t"]);
                _info.TurnPlayerId = data["t"] == 0 ? 1 : 0;
            } catch (Exception e) {
                Debug.LogException(e);
            }
            var status = _info.PlayerId != _info.TurnPlayerId ? "Wait for the opponent's step" : "Make your step";
            SetStatus(status);
        });
        Connector.Use().On(Consts.EXIT_GAME, (response) => {
            SetStatus("Opponent has left the game");
        });
    }

    /// <summary>
    /// Unsubscrive
    /// </summary>
    void OnDisable() {
        Connector.Use().Off(Consts.ROUND);
        Connector.Use().Off(Consts.TURN);
        Connector.Use().Off(Consts.EXIT_GAME);
    }

    /// <summary>
    /// Clear play field
    /// </summary>
    void Clear() {
        for (var x = 0; x < ROW_MAX_COUNT; x++) {
            for (var y = 0; y < ROW_MAX_COUNT; y++) {
                SetItem(x, y, ItemType.None);
            }
        }
    }

    /// <summary>
    /// Set score 
    /// </summary>
    void SetScore() {
        ScoreText.text = $"{_info.Score[0]}:{_info.Score[1]}";
    }

    /// <summary>
    /// Set status message
    /// </summary>
    /// <param name="message">Message text</param>
    void SetStatus(string message) {
        StatusText.text = message;
    }

    /// <summary>
    /// Exit from game
    /// </summary>
    public void Exit() {
        var json = new JsonObject();
        json["g"] = _info.GameId;
        json["p"] = _info.PlayerId;
        var data = Encoding.UTF8.GetBytes(json.ToJsonString());
        Connector.Use().Request(Consts.EXIT_GAME, data, (data) => {
            SceneManager.LoadScene(Consts.MENU_SCENE);
        });
    }

    /// <summary>
    /// Set play item
    /// </summary>
    /// <param name="x">X coordinate</param>
    /// <param name="y">Y coordinate</param>
    /// <param name="type">Type of item</param>
    public void SetItem(int x, int y, ItemType type) {
        switch (type) {
            case ItemType.Cross:
                _field[x, y].color = CrossColor;
                _field[x, y].sprite = CrossIcon;
                break;
            case ItemType.Circle:
                _field[x, y].color = CircleColor;
                _field[x, y].sprite = CircleIcon;
                break;
            case ItemType.None:
                _field[x, y].color = Color.white;
                _field[x, y].sprite = null;
                break;
            default:
                break;
        }
    }

    /// <summary>
    /// Make game step
    /// </summary>
    /// <param name="x">X coordinate</param>
    /// <param name="y">Y coordinate</param>
    public void MakeTurn(int x, int y) {
        if (_info.PlayerId != _info.TurnPlayerId) {
            return;
        }
        var json = new JsonObject();
        json["g"] = _info.GameId;
        json["p"] = _info.PlayerId;
        json["x"] = x;
        json["y"] = y;
        var data = Encoding.UTF8.GetBytes(json.ToJsonString());
        Connector.Use().Request(Consts.TURN, data, (response) => {
            if (!string.IsNullOrEmpty(response.Error)) {
                SetStatus(response.Error);
            }
        });
    }
}

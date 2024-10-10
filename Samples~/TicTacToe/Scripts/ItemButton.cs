using UnityEngine;
using UnityEngine.EventSystems;

public class ItemButton : MonoBehaviour, IPointerClickHandler {

    /// <summary>
    /// Cache controller
    /// </summary>
    GameController _controller = null;

    /// <summary>
    /// Cached X coordinate
    /// </summary>
    int _x = -1;

    /// <summary>
    /// Cached Y coordinate
    /// </summary>
    int _y = -1;

    /// <summary>
    /// Init item button
    /// </summary>
    /// <param name="controller">Parent controller</param>
    /// <param name="x">X coord</param>
    /// <param name="y">Y coord</param> 
    public void Init(GameController controller, int x, int y) {
        _controller = controller;
        _x = x;
        _y = y;
    }

    /// <summary>
    /// Action on click
    /// </summary>
    public void OnPointerClick(PointerEventData data) {
        _controller.MakeTurn(_x, _y);
    }
}

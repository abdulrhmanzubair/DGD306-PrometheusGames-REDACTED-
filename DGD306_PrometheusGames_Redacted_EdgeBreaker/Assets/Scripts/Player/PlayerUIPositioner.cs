using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Alternative: Attach this to each player's health canvas for automatic positioning
/// Use this if you don't want to modify PlayerHealthSystem
/// </summary>
public class PlayerUIPositioner : MonoBehaviour
{
    [Header("UI Positioning Settings")]
    [SerializeField] private bool autoPosition = true;
    [SerializeField] private UILayoutType layoutType = UILayoutType.HorizontalSplit;
    [SerializeField] private float marginFromEdge = 50f;

    [Header("Custom Positioning (if not auto)")]
    [SerializeField] private Vector2 customPosition = Vector2.zero;
    [SerializeField] private TextAnchor customAnchor = TextAnchor.UpperLeft;

    private Canvas canvas;
    private RectTransform rectTransform;
    private int playerIndex = -1;

    public enum UILayoutType
    {
        HorizontalSplit,    // Player 1 left, Player 2 right
        VerticalSplit,      // Player 1 top, Player 2 bottom
        Corners,            // Player 1 top-left, Player 2 top-right
        CornersBottom       // Player 1 bottom-left, Player 2 bottom-right
    }

    void Awake()
    {
        canvas = GetComponent<Canvas>();
        rectTransform = GetComponent<RectTransform>();

        if (canvas == null)
        {
            Debug.LogError($"PlayerUIPositioner requires a Canvas component on {gameObject.name}");
            return;
        }

        // Ensure canvas is in Screen Space - Overlay mode
        if (canvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }
    }

    void Start()
    {
        // Try to get player index from parent player
        FindPlayerIndex();

        if (autoPosition && playerIndex >= 0)
        {
            PositionUIForPlayer();
        }
        else if (!autoPosition)
        {
            SetCustomPosition();
        }
    }

    void FindPlayerIndex()
    {
        // Look for player controllers in parent objects
        Transform current = transform.parent;
        while (current != null && playerIndex < 0)
        {
            // Check for melee controller
            Player_Melee_Controller1 meleeController = current.GetComponent<Player_Melee_Controller1>();
            if (meleeController != null)
            {
                playerIndex = meleeController.PlayerIndex;
                break;
            }

            // Check for gunner controller
            PlayerController gunnerController = current.GetComponent<PlayerController>();
            if (gunnerController != null)
            {
                playerIndex = gunnerController.PlayerIndex;
                break;
            }

            current = current.parent;
        }

        if (playerIndex < 0)
        {
            Debug.LogWarning($"Could not find player index for UI {gameObject.name}. Defaulting to Player 0.");
            playerIndex = 0;
        }

        Debug.Log($"UI {gameObject.name} assigned to Player {playerIndex}");
    }

    void PositionUIForPlayer()
    {
        if (rectTransform == null) return;

        Vector2 anchorMin, anchorMax, anchoredPosition;

        switch (layoutType)
        {
            case UILayoutType.HorizontalSplit:
                SetHorizontalSplitPosition(out anchorMin, out anchorMax, out anchoredPosition);
                break;

            case UILayoutType.VerticalSplit:
                SetVerticalSplitPosition(out anchorMin, out anchorMax, out anchoredPosition);
                break;

            case UILayoutType.Corners:
                SetCornersPosition(out anchorMin, out anchorMax, out anchoredPosition);
                break;

            case UILayoutType.CornersBottom:
                SetCornersBottomPosition(out anchorMin, out anchorMax, out anchoredPosition);
                break;

            default:
                SetHorizontalSplitPosition(out anchorMin, out anchorMax, out anchoredPosition);
                break;
        }

        rectTransform.anchorMin = anchorMin;
        rectTransform.anchorMax = anchorMax;
        rectTransform.anchoredPosition = anchoredPosition;

        // Set sorting order to prevent overlap
        canvas.sortingOrder = playerIndex;
    }

    void SetHorizontalSplitPosition(out Vector2 anchorMin, out Vector2 anchorMax, out Vector2 anchoredPosition)
    {
        if (playerIndex == 0) // Player 1 - Left side
        {
            anchorMin = new Vector2(0f, 1f);  // Top-left anchor
            anchorMax = new Vector2(0f, 1f);
            anchoredPosition = new Vector2(marginFromEdge, -marginFromEdge);
        }
        else // Player 2 - Right side
        {
            anchorMin = new Vector2(1f, 1f);  // Top-right anchor
            anchorMax = new Vector2(1f, 1f);
            anchoredPosition = new Vector2(-marginFromEdge, -marginFromEdge);
        }
    }

    void SetVerticalSplitPosition(out Vector2 anchorMin, out Vector2 anchorMax, out Vector2 anchoredPosition)
    {
        if (playerIndex == 0) // Player 1 - Top
        {
            anchorMin = new Vector2(0.5f, 1f);  // Top-center anchor
            anchorMax = new Vector2(0.5f, 1f);
            anchoredPosition = new Vector2(0f, -marginFromEdge);
        }
        else // Player 2 - Bottom
        {
            anchorMin = new Vector2(0.5f, 0f);  // Bottom-center anchor
            anchorMax = new Vector2(0.5f, 0f);
            anchoredPosition = new Vector2(0f, marginFromEdge);
        }
    }

    void SetCornersPosition(out Vector2 anchorMin, out Vector2 anchorMax, out Vector2 anchoredPosition)
    {
        if (playerIndex == 0) // Player 1 - Top-left
        {
            anchorMin = new Vector2(0f, 1f);
            anchorMax = new Vector2(0f, 1f);
            anchoredPosition = new Vector2(marginFromEdge, -marginFromEdge);
        }
        else // Player 2 - Top-right
        {
            anchorMin = new Vector2(1f, 1f);
            anchorMax = new Vector2(1f, 1f);
            anchoredPosition = new Vector2(-marginFromEdge, -marginFromEdge);
        }
    }

    void SetCornersBottomPosition(out Vector2 anchorMin, out Vector2 anchorMax, out Vector2 anchoredPosition)
    {
        if (playerIndex == 0) // Player 1 - Bottom-left
        {
            anchorMin = new Vector2(0f, 0f);
            anchorMax = new Vector2(0f, 0f);
            anchoredPosition = new Vector2(marginFromEdge, marginFromEdge);
        }
        else // Player 2 - Bottom-right
        {
            anchorMin = new Vector2(1f, 0f);
            anchorMax = new Vector2(1f, 0f);
            anchoredPosition = new Vector2(-marginFromEdge, marginFromEdge);
        }
    }

    void SetCustomPosition()
    {
        if (rectTransform == null) return;

        // Convert TextAnchor to anchor values
        Vector2 anchorMin, anchorMax;
        GetAnchorsFromTextAnchor(customAnchor, out anchorMin, out anchorMax);

        rectTransform.anchorMin = anchorMin;
        rectTransform.anchorMax = anchorMax;
        rectTransform.anchoredPosition = customPosition;
    }

    void GetAnchorsFromTextAnchor(TextAnchor anchor, out Vector2 anchorMin, out Vector2 anchorMax)
    {
        switch (anchor)
        {
            case TextAnchor.UpperLeft:
                anchorMin = anchorMax = new Vector2(0f, 1f);
                break;
            case TextAnchor.UpperCenter:
                anchorMin = anchorMax = new Vector2(0.5f, 1f);
                break;
            case TextAnchor.UpperRight:
                anchorMin = anchorMax = new Vector2(1f, 1f);
                break;
            case TextAnchor.MiddleLeft:
                anchorMin = anchorMax = new Vector2(0f, 0.5f);
                break;
            case TextAnchor.MiddleCenter:
                anchorMin = anchorMax = new Vector2(0.5f, 0.5f);
                break;
            case TextAnchor.MiddleRight:
                anchorMin = anchorMax = new Vector2(1f, 0.5f);
                break;
            case TextAnchor.LowerLeft:
                anchorMin = anchorMax = new Vector2(0f, 0f);
                break;
            case TextAnchor.LowerCenter:
                anchorMin = anchorMax = new Vector2(0.5f, 0f);
                break;
            case TextAnchor.LowerRight:
                anchorMin = anchorMax = new Vector2(1f, 0f);
                break;
            default:
                anchorMin = anchorMax = new Vector2(0f, 1f);
                break;
        }
    }

    // Public methods for manual control
    public void SetPlayerIndex(int index)
    {
        playerIndex = index;
        if (autoPosition)
        {
            PositionUIForPlayer();
        }
    }

    public void RefreshPosition()
    {
        if (autoPosition && playerIndex >= 0)
        {
            PositionUIForPlayer();
        }
    }
}
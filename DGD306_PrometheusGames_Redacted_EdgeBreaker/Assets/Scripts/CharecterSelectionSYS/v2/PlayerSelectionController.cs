using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[RequireComponent(typeof(PlayerInput))]
public class PlayerSelectionController : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Image meleeHighlight;
    [SerializeField] private Image gunnerHighlight;
    [SerializeField] private GameObject readyText;

    private int playerIndex;
    private bool isReady = false;
    private bool hasSelectedCharacter = false;
    private CharacterSelectionManager selectionManager;

    private float navCooldown = 0.25f;
    private float lastNavTime = -1f;

    void Start()
    {
        var input = GetComponent<PlayerInput>();
        playerIndex = input.playerIndex;

        selectionManager = CharacterSelectionManager.Instance;
        if (selectionManager == null)
        {
            Debug.LogError("CharacterSelectionManager missing!");
            enabled = false;
            return;
        }

        ResetHighlights();

        if (readyText) readyText.SetActive(false);
    }

    public void OnMove(InputAction.CallbackContext context)
    {
        if (isReady || !context.performed || hasSelectedCharacter) return;
        Vector2 input = context.ReadValue<Vector2>();

        if (Time.time - lastNavTime < navCooldown) return;

        if (input.x > 0.5f)
        {
            SelectCharacter(CharacterSelectionManager.CharacterType.Melee);
            lastNavTime = Time.time;
        }
        else if (input.x < -0.5f)
        {
            SelectCharacter(CharacterSelectionManager.CharacterType.Gunner);
            lastNavTime = Time.time;
        }
    }

    public void OnConfirm(InputAction.CallbackContext context)
    {
        if (!hasSelectedCharacter || isReady || !context.performed) return;

        isReady = true;
        if (readyText) readyText.SetActive(true);
        Debug.Log($"Player {playerIndex} confirmed!");

        if (SelectionCoordinator.Instance)
            SelectionCoordinator.Instance.PlayerReady();
        else
            Invoke(nameof(FallbackStartGame), 1.5f);
    }

    private void SelectCharacter(CharacterSelectionManager.CharacterType character)
    {
        hasSelectedCharacter = true;
        selectionManager.SetPlayerSelection(playerIndex, character);

        ResetHighlights();

        if (character == CharacterSelectionManager.CharacterType.Melee && meleeHighlight)
            meleeHighlight.enabled = true;
        if (character == CharacterSelectionManager.CharacterType.Gunner && gunnerHighlight)
            gunnerHighlight.enabled = true;

        Debug.Log($"Player {playerIndex} selected: {character}");
    }

    private void ResetHighlights()
    {
        if (meleeHighlight) meleeHighlight.enabled = false;
        if (gunnerHighlight) gunnerHighlight.enabled = false;
    }

    private void FallbackStartGame()
    {
        SceneManager.LoadScene("Level1");
    }
}

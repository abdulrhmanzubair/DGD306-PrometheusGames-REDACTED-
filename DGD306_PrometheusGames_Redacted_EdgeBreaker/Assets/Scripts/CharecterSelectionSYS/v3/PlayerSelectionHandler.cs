using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class PlayerSelectionHandler : MonoBehaviour
{
    public Image meleeHighlight;
    public Image gunnerHighlight;
    public GameObject readyIndicator;

    private int playerIndex;
    private bool isReady = false;

    private CharacterSelectionData.CharacterType selectedCharacter = CharacterSelectionData.CharacterType.Melee;

    void Start()
    {
        playerIndex = GetComponent<PlayerInput>().playerIndex;
        UpdateHighlight();
        readyIndicator.SetActive(false);
    }

    public void OnMove(InputAction.CallbackContext ctx)
    {
        if (isReady || !ctx.performed) return;

        Vector2 input = ctx.ReadValue<Vector2>();
        if (input.x > 0.5f)
        {
            selectedCharacter = CharacterSelectionData.CharacterType.Melee;
            UpdateHighlight();
        }
        else if (input.x < -0.5f)
        {
            selectedCharacter = CharacterSelectionData.CharacterType.Gunner;
            UpdateHighlight();
        }
    }

    public void OnSubmit(InputAction.CallbackContext ctx)
    {
        if (!ctx.performed || isReady) return;

        isReady = true;
        CharacterSelectionData.Instance.SetSelection(playerIndex, selectedCharacter);
        readyIndicator.SetActive(true);

        CharacterSelectManager.Instance.PlayerReady();
    }

    private void UpdateHighlight()
    {
        meleeHighlight.enabled = (selectedCharacter == CharacterSelectionData.CharacterType.Melee);
        gunnerHighlight.enabled = (selectedCharacter == CharacterSelectionData.CharacterType.Gunner);
    }
}

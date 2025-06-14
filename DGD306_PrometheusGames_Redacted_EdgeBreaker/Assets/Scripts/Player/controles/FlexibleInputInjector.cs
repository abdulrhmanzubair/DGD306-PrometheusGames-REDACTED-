// FlexibleInputInjector.cs - Helper component for advanced integration
using UnityEngine;

/// <summary>
/// Advanced input injector that provides a clean API for controllers
/// This gets added automatically by AutoFlexibleInputSetup
/// </summary>
public class FlexibleInputInjector : MonoBehaviour
{
    [Header("Configuration")]
    public int playerIndex;
    public string controllerType = "Unknown";

    [Header("Status")]
    public bool isInjecting = false;
    public string currentInputMethod = "none";

    private SimpleFlexibleInput flexInput;

    void Start()
    {
        // Get reference to the flexible input component
        flexInput = GetComponent<SimpleFlexibleInput>();

        if (flexInput != null)
        {
            isInjecting = true;
            Debug.Log($"🔌 FlexibleInputInjector active for {controllerType} Player {playerIndex}");
        }
        else
        {
            Debug.LogError($"❌ FlexibleInputInjector: No SimpleFlexibleInput found on {controllerType} Player {playerIndex}");
        }
    }

    void Update()
    {
        if (!isInjecting || flexInput == null) return;
        currentInputMethod = flexInput.currentInputMethod;
    }

    // Clean API for controllers to use
    public Vector2 GetMoveInput() => flexInput?.moveInput ?? Vector2.zero;
    public Vector2 GetAimInput() => flexInput?.aimInput ?? Vector2.zero;
    public bool GetJumpPressed() => flexInput?.jumpPressed ?? false;
    public bool GetJumpHeld() => flexInput?.jumpHeld ?? false;
    public bool GetAction1Pressed() => flexInput?.action1Pressed ?? false;
    public bool GetAction2Pressed() => flexInput?.action2Pressed ?? false;
    public bool GetAction1Held() => flexInput?.action1Held ?? false;
    public bool GetAction2Held() => flexInput?.action2Held ?? false;
    public string GetCurrentInputMethod() => flexInput?.currentInputMethod ?? "none";
}

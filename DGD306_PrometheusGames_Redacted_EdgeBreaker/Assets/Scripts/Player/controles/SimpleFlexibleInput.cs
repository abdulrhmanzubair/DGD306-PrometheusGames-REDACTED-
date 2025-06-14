// SimpleFlexibleInput.cs - Main flexible input component
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Simple flexible input system that detects and switches between input methods in real-time
/// This is the core component that handles all input detection and switching
/// </summary>
public class SimpleFlexibleInput : MonoBehaviour
{
    [Header("Player Settings")]
    public int playerIndex = 0;

    [Header("Real-time Input Detection")]
    public bool enableRealTimeSwitching = true;
    public float switchCooldown = 0.3f;

    // Public input state that controllers can read from
    [HideInInspector] public Vector2 moveInput;
    [HideInInspector] public Vector2 aimInput;
    [HideInInspector] public bool jumpPressed;
    [HideInInspector] public bool jumpHeld;
    [HideInInspector] public bool action1Pressed; // Fire1/Guard
    [HideInInspector] public bool action2Pressed; // Fire2/Dash
    [HideInInspector] public bool action1Held;
    [HideInInspector] public bool action2Held;

    // Current active input method
    public string currentInputMethod = "auto";

    private Keyboard keyboard;
    private float lastSwitchTime;
    private Gamepad currentGamepad;
    private bool wasAction1PressedLastFrame;
    private bool wasAction2PressedLastFrame;
    private bool wasJumpPressedLastFrame;

    void Start()
    {
        keyboard = Keyboard.current;

        // Try to assign a gamepad
        var gamepads = Gamepad.all;
        if (gamepads.Count > playerIndex)
        {
            currentGamepad = gamepads[playerIndex];
        }

        Debug.Log($"SimpleFlexibleInput: Player {playerIndex} initialized with gamepad: {currentGamepad?.name ?? "None"}");
    }

    void Update()
    {
        // Store previous frame states for press detection
        bool prevAction1 = action1Pressed;
        bool prevAction2 = action2Pressed;
        bool prevJump = jumpPressed;

        // Reset press states (but keep held states)
        jumpPressed = false;
        action1Pressed = false;
        action2Pressed = false;

        bool foundInput = false;

        // Check gamepad first (higher priority)
        if (currentGamepad != null && CheckGamepadInput())
        {
            foundInput = true;
            SwitchToMethod("gamepad");
        }

        // Check keyboard schemes
        if (!foundInput || enableRealTimeSwitching)
        {
            if (CheckKeyboardInput(playerIndex == 0 ? "WASD" : "Arrows"))
            {
                foundInput = true;
                SwitchToMethod(playerIndex == 0 ? "WASD" : "Arrows");
            }
            else if (enableRealTimeSwitching)
            {
                // Try other keyboard schemes
                if (CheckKeyboardInput("WASD"))
                {
                    SwitchToMethod("WASD");
                }
                else if (CheckKeyboardInput("Arrows"))
                {
                    SwitchToMethod("Arrows");
                }
            }
        }

        // Detect press events (transition from false to true)
        if (action1Held && !wasAction1PressedLastFrame)
            action1Pressed = true;
        if (action2Held && !wasAction2PressedLastFrame)
            action2Pressed = true;
        if (jumpHeld && !wasJumpPressedLastFrame)
            jumpPressed = true;

        wasAction1PressedLastFrame = action1Held;
        wasAction2PressedLastFrame = action2Held;
        wasJumpPressedLastFrame = jumpHeld;
    }

    bool CheckGamepadInput()
    {
        bool hasInput = false;

        Vector2 leftStick = currentGamepad.leftStick.ReadValue();
        Vector2 dpad = currentGamepad.dpad.ReadValue();
        moveInput = leftStick.magnitude > 0.1f ? leftStick : dpad;

        Vector2 rightStick = currentGamepad.rightStick.ReadValue();
        aimInput = rightStick.magnitude > 0.1f ? rightStick : moveInput;

        if (moveInput.magnitude > 0.1f) hasInput = true;

        // Button states
        jumpHeld = currentGamepad.buttonSouth.isPressed;
        action1Held = currentGamepad.rightTrigger.isPressed || currentGamepad.buttonWest.isPressed;
        action2Held = currentGamepad.rightShoulder.isPressed || currentGamepad.buttonNorth.isPressed;

        if (jumpHeld || action1Held || action2Held) hasInput = true;

        return hasInput;
    }

    bool CheckKeyboardInput(string scheme)
    {
        if (keyboard == null) return false;

        bool hasInput = false;
        Vector2 movement = Vector2.zero;

        if (scheme == "WASD")
        {
            if (keyboard.aKey.isPressed) { movement.x -= 1; hasInput = true; }
            if (keyboard.dKey.isPressed) { movement.x += 1; hasInput = true; }
            if (keyboard.wKey.isPressed) { movement.y += 1; hasInput = true; }
            if (keyboard.sKey.isPressed) { movement.y -= 1; hasInput = true; }

            jumpHeld = keyboard.spaceKey.isPressed;
            action1Held = keyboard.leftCtrlKey.isPressed;
            action2Held = keyboard.leftShiftKey.isPressed;
        }
        else if (scheme == "Arrows")
        {
            if (keyboard.leftArrowKey.isPressed) { movement.x -= 1; hasInput = true; }
            if (keyboard.rightArrowKey.isPressed) { movement.x += 1; hasInput = true; }
            if (keyboard.upArrowKey.isPressed) { movement.y += 1; hasInput = true; }
            if (keyboard.downArrowKey.isPressed) { movement.y -= 1; hasInput = true; }

            jumpHeld = keyboard.enterKey.isPressed;
            action1Held = keyboard.rightCtrlKey.isPressed;
            action2Held = keyboard.rightShiftKey.isPressed;
        }

        if (hasInput || jumpHeld || action1Held || action2Held)
        {
            moveInput = movement;
            if (movement.magnitude > 0.1f)
                aimInput = movement;
            hasInput = true;
        }

        return hasInput;
    }

    void SwitchToMethod(string method)
    {
        if (currentInputMethod != method && Time.time - lastSwitchTime >= switchCooldown)
        {
            currentInputMethod = method;
            lastSwitchTime = Time.time;
            Debug.Log($"🎮 Player {playerIndex} switched to {method}");
        }
    }

    void OnGUI()
    {
        if (Application.isEditor)
        {
            GUI.Label(new Rect(10, playerIndex * 60 + 200, 300, 20),
                $"Player {playerIndex}: {currentInputMethod}");
            GUI.Label(new Rect(10, playerIndex * 60 + 220, 300, 20),
                $"Move: {moveInput:F1} | Actions: {action1Held}/{action2Held}");
        }
    }
}

// SimpleFlexibleInput.cs - Completely isolated per-player input system
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

/// <summary>
/// Completely isolated input system - each player gets their own gamepad + keyboard controls
/// No sharing, no cross-contamination between players
/// </summary>
public class SimpleFlexibleInput : MonoBehaviour
{
    [Header("Player Settings")]
    public int playerIndex = 0;

    [Header("Input Settings")]
    public bool enableGamepad = true;
    public bool enableKeyboard = true;
    public float inputDeadzone = 0.1f;

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
    public string currentInputMethod = "none";

    // Static gamepad assignment tracking
    private static Dictionary<int, int> gamepadAssignments = new Dictionary<int, int>(); // playerIndex -> gamepadIndex
    private static List<int> availableGamepads = new List<int>();

    // Per-player dedicated controls
    private Gamepad assignedGamepad;
    private int assignedGamepadIndex = -1;
    private Keyboard keyboard;

    // Keyboard schemes per player
    private Dictionary<string, Key[]> keyboardSchemes;

    // Press detection
    private bool wasAction1PressedLastFrame;
    private bool wasAction2PressedLastFrame;
    private bool wasJumpPressedLastFrame;

    void Start()
    {
        keyboard = Keyboard.current;

        // Initialize keyboard schemes
        InitializeKeyboardSchemes();

        // Try to assign a gamepad to this player
        AssignGamepadToPlayer();

        // Set initial input method
        if (assignedGamepad != null)
        {
            currentInputMethod = $"Gamepad{assignedGamepadIndex}";
        }
        else
        {
            currentInputMethod = GetKeyboardMethodName();
        }

        Debug.Log($"🎮 Player {playerIndex} initialized:");
        Debug.Log($"   Gamepad: {assignedGamepad?.name ?? "None"}");
        Debug.Log($"   Keyboard: {(keyboard != null ? "Available" : "None")}");
        Debug.Log($"   Input Method: {currentInputMethod}");
    }

    void InitializeKeyboardSchemes()
    {
        keyboardSchemes = new Dictionary<string, Key[]>();

        // Player 0: WASD + Space/Ctrl/Shift
        if (playerIndex == 0)
        {
            keyboardSchemes["move"] = new Key[] { Key.A, Key.D, Key.W, Key.S }; // left, right, up, down
            keyboardSchemes["jump"] = new Key[] { Key.Space };
            keyboardSchemes["action1"] = new Key[] { Key.LeftCtrl };
            keyboardSchemes["action2"] = new Key[] { Key.LeftShift };
        }
        // Player 1: Arrow Keys + Enter/RCtrl/RShift
        else if (playerIndex == 1)
        {
            keyboardSchemes["move"] = new Key[] { Key.LeftArrow, Key.RightArrow, Key.UpArrow, Key.DownArrow };
            keyboardSchemes["jump"] = new Key[] { Key.Enter };
            keyboardSchemes["action1"] = new Key[] { Key.RightCtrl };
            keyboardSchemes["action2"] = new Key[] { Key.RightShift };
        }
        // Player 2+: IJKL scheme
        else
        {
            keyboardSchemes["move"] = new Key[] { Key.J, Key.L, Key.I, Key.K };
            keyboardSchemes["jump"] = new Key[] { Key.H };
            keyboardSchemes["action1"] = new Key[] { Key.N };
            keyboardSchemes["action2"] = new Key[] { Key.M };
        }
    }

    void AssignGamepadToPlayer()
    {
        // Update available gamepads list
        RefreshAvailableGamepads();

        // If this player already has a gamepad assigned, check if it's still connected
        if (gamepadAssignments.ContainsKey(playerIndex))
        {
            int gamepadIndex = gamepadAssignments[playerIndex];
            if (gamepadIndex < Gamepad.all.Count)
            {
                assignedGamepad = Gamepad.all[gamepadIndex];
                assignedGamepadIndex = gamepadIndex;
                Debug.Log($"Player {playerIndex} keeping assigned gamepad {gamepadIndex}: {assignedGamepad.name}");
                return;
            }
            else
            {
                // Gamepad disconnected, remove assignment
                gamepadAssignments.Remove(playerIndex);
                assignedGamepad = null;
                assignedGamepadIndex = -1;
                Debug.Log($"Player {playerIndex} gamepad disconnected, falling back to keyboard");
            }
        }

        // Try to assign an available gamepad
        for (int i = 0; i < Gamepad.all.Count; i++)
        {
            if (availableGamepads.Contains(i))
            {
                // Assign this gamepad to this player
                gamepadAssignments[playerIndex] = i;
                availableGamepads.Remove(i);
                assignedGamepad = Gamepad.all[i];
                assignedGamepadIndex = i;
                Debug.Log($"Player {playerIndex} assigned gamepad {i}: {assignedGamepad.name}");
                return;
            }
        }

        Debug.Log($"Player {playerIndex} no gamepad available, using keyboard: {GetKeyboardMethodName()}");
    }

    void RefreshAvailableGamepads()
    {
        availableGamepads.Clear();

        // Add all gamepads that aren't assigned to any player
        for (int i = 0; i < Gamepad.all.Count; i++)
        {
            bool isAssigned = false;
            foreach (var assignment in gamepadAssignments)
            {
                if (assignment.Value == i)
                {
                    isAssigned = true;
                    break;
                }
            }

            if (!isAssigned)
            {
                availableGamepads.Add(i);
            }
        }
    }

    void Update()
    {
        // Check for new gamepads periodically
        if (assignedGamepad == null && Time.frameCount % 60 == 0) // Check once per second
        {
            AssignGamepadToPlayer();
        }

        // Store previous frame states for press detection
        wasAction1PressedLastFrame = action1Held;
        wasAction2PressedLastFrame = action2Held;
        wasJumpPressedLastFrame = jumpHeld;

        // Reset press states
        jumpPressed = false;
        action1Pressed = false;
        action2Pressed = false;

        // Clear input
        moveInput = Vector2.zero;
        aimInput = Vector2.zero;
        jumpHeld = false;
        action1Held = false;
        action2Held = false;

        bool hasInput = false;

        // PRIORITY 1: Check assigned gamepad input (if available)
        if (enableGamepad && assignedGamepad != null)
        {
            if (CheckGamepadInput())
            {
                hasInput = true;
                // Update current method to show gamepad is active
                string gamepadMethod = $"Gamepad{assignedGamepadIndex}";
                if (currentInputMethod != gamepadMethod)
                {
                    currentInputMethod = gamepadMethod;
                    Debug.Log($"🎮 Player {playerIndex} using {currentInputMethod}");
                }
            }
        }

        // PRIORITY 2: Check keyboard input (if no gamepad input or as supplement)
        if (enableKeyboard && keyboard != null && (!hasInput || assignedGamepad == null))
        {
            if (CheckKeyboardInput())
            {
                if (!hasInput) // Only set as primary input method if no gamepad
                {
                    string keyboardMethod = GetKeyboardMethodName();
                    if (currentInputMethod != keyboardMethod)
                    {
                        currentInputMethod = keyboardMethod;
                        Debug.Log($"⌨️ Player {playerIndex} using {currentInputMethod}");
                    }
                }
                hasInput = true;
            }
        }

        // Detect press events (transition from false to true)
        if (action1Held && !wasAction1PressedLastFrame)
            action1Pressed = true;
        if (action2Held && !wasAction2PressedLastFrame)
            action2Pressed = true;
        if (jumpHeld && !wasJumpPressedLastFrame)
            jumpPressed = true;
    }

    bool CheckGamepadInput()
    {
        if (assignedGamepad == null) return false;

        bool hasInput = false;

        // Movement
        Vector2 leftStick = assignedGamepad.leftStick.ReadValue();
        Vector2 dpad = assignedGamepad.dpad.ReadValue();
        Vector2 gamepadMove = leftStick.magnitude > inputDeadzone ? leftStick : dpad;

        if (gamepadMove.magnitude > inputDeadzone)
        {
            moveInput = gamepadMove;
            hasInput = true;
        }

        // Aiming (right stick or movement)
        Vector2 rightStick = assignedGamepad.rightStick.ReadValue();
        Vector2 gamepadAim = rightStick.magnitude > inputDeadzone ? rightStick : gamepadMove;

        if (gamepadAim.magnitude > inputDeadzone)
        {
            aimInput = gamepadAim;
        }

        // Buttons
        if (assignedGamepad.buttonSouth.isPressed) { jumpHeld = true; hasInput = true; }
        if (assignedGamepad.rightTrigger.isPressed || assignedGamepad.buttonWest.isPressed) { action1Held = true; hasInput = true; }
        if (assignedGamepad.rightShoulder.isPressed || assignedGamepad.buttonNorth.isPressed) { action2Held = true; hasInput = true; }

        return hasInput;
    }

    bool CheckKeyboardInput()
    {
        if (keyboard == null || keyboardSchemes == null) return false;

        bool hasInput = false;
        Vector2 keyboardMove = Vector2.zero;

        // Movement
        if (keyboardSchemes.ContainsKey("move"))
        {
            Key[] moveKeys = keyboardSchemes["move"];
            if (moveKeys.Length >= 4)
            {
                if (keyboard[moveKeys[0]].isPressed) { keyboardMove.x -= 1; hasInput = true; } // left
                if (keyboard[moveKeys[1]].isPressed) { keyboardMove.x += 1; hasInput = true; } // right
                if (keyboard[moveKeys[2]].isPressed) { keyboardMove.y += 1; hasInput = true; } // up
                if (keyboard[moveKeys[3]].isPressed) { keyboardMove.y -= 1; hasInput = true; } // down
            }
        }

        // Only use keyboard movement if no gamepad movement OR gamepad not assigned
        if (assignedGamepad == null || moveInput.magnitude < inputDeadzone)
        {
            if (keyboardMove.magnitude > 0)
            {
                moveInput = keyboardMove.normalized;
                aimInput = keyboardMove.normalized;
            }
        }

        // Buttons (keyboard can always supplement gamepad buttons)
        if (keyboardSchemes.ContainsKey("jump") && keyboard[keyboardSchemes["jump"][0]].isPressed)
        {
            jumpHeld = true;
            hasInput = true;
        }

        if (keyboardSchemes.ContainsKey("action1") && keyboard[keyboardSchemes["action1"][0]].isPressed)
        {
            action1Held = true;
            hasInput = true;
        }

        if (keyboardSchemes.ContainsKey("action2") && keyboard[keyboardSchemes["action2"][0]].isPressed)
        {
            action2Held = true;
            hasInput = true;
        }

        return hasInput;
    }

    string GetKeyboardMethodName()
    {
        if (playerIndex == 0) return "WASD";
        else if (playerIndex == 1) return "Arrows";
        else return "IJKL";
    }

    void OnDestroy()
    {
        // Release gamepad assignment when destroyed
        if (gamepadAssignments.ContainsKey(playerIndex))
        {
            gamepadAssignments.Remove(playerIndex);
            RefreshAvailableGamepads();
        }
    }

    void OnGUI()
    {
        if (Application.isEditor)
        {
            float yOffset = playerIndex * 120 + 10;

            GUI.Box(new Rect(10, yOffset, 350, 110), $"Player {playerIndex} Input");

            GUI.Label(new Rect(20, yOffset + 25, 330, 20),
                $"Method: {currentInputMethod}");
            GUI.Label(new Rect(20, yOffset + 45, 330, 20),
                $"Move: {moveInput:F2} | Aim: {aimInput:F2}");
            GUI.Label(new Rect(20, yOffset + 65, 330, 20),
                $"Jump: {jumpHeld} | A1: {action1Held} | A2: {action2Held}");
            GUI.Label(new Rect(20, yOffset + 85, 330, 20),
                $"Gamepad: {assignedGamepad?.name ?? "None"}");
        }
    }

    // Public methods for debugging
    [ContextMenu("Force Reassign Gamepad")]
    public void ForceReassignGamepad()
    {
        if (gamepadAssignments.ContainsKey(playerIndex))
        {
            gamepadAssignments.Remove(playerIndex);
        }
        assignedGamepad = null;
        assignedGamepadIndex = -1;
        AssignGamepadToPlayer();
    }

    public static void ReleaseAllGamepadAssignments()
    {
        gamepadAssignments.Clear();
        availableGamepads.Clear();
        Debug.Log("All gamepad assignments released");
    }
}
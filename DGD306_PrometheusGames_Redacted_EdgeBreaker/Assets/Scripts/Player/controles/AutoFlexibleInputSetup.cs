// Complete AutoFlexibleInputSetup.cs - Fixed version
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

/// <summary>
/// ONE-CLICK SETUP: Add this to any GameObject in your scene and it configures everything automatically
/// </summary>
public class AutoFlexibleInputSetup : MonoBehaviour
{
    [Header("Auto Setup Configuration")]
    [Tooltip("Automatically setup when scene starts")]
    public bool autoSetupOnStart = true;

    [Tooltip("Enable real-time switching between input methods")]
    public bool enableRealTimeSwitching = true;

    [Tooltip("Show debug UI for testing")]
    public bool showDebugUI = true;

    [Tooltip("Switch cooldown to prevent jitter")]
    public float switchCooldown = 0.3f;

    [Header("Status")]
    public bool isSetupComplete = false;
    public int playersConfigured = 0;

    void Start()
    {
        if (autoSetupOnStart)
        {
            StartCoroutine(SetupAfterFrame());
        }
    }

    IEnumerator SetupAfterFrame()
    {
        // Wait one frame to ensure all players are spawned
        yield return null;
        SetupEverything();
    }

    [ContextMenu("Setup Everything")]
    public void SetupEverything()
    {
        Debug.Log("🚀 AUTO FLEXIBLE INPUT SETUP STARTING...");

        // Configure all existing players
        ConfigureExistingPlayers();

        // Verify setup
        VerifySetup();

        isSetupComplete = true;
        Debug.Log($"✅ SETUP COMPLETE! {playersConfigured} players configured for flexible input.");
    }

    void ConfigureExistingPlayers()
    {
        playersConfigured = 0;

        // Find all player controllers
        PlayerController[] gunners = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        Player_Melee_Controller1[] meleeControllers = FindObjectsByType<Player_Melee_Controller1>(FindObjectsSortMode.None);

        Debug.Log($"🔍 Found {gunners.Length} gunner controllers, {meleeControllers.Length} melee controllers");

        // Configure gunner controllers
        foreach (PlayerController gunner in gunners)
        {
            ConfigurePlayerForFlexibleInput(gunner.gameObject, gunner.PlayerIndex, "Gunner");
            playersConfigured++;
        }

        // Configure melee controllers
        foreach (Player_Melee_Controller1 melee in meleeControllers)
        {
            ConfigurePlayerForFlexibleInput(melee.gameObject, melee.PlayerIndex, "Melee");
            playersConfigured++;
        }
    }

    void ConfigurePlayerForFlexibleInput(GameObject playerObject, int playerIndex, string controllerType)
    {
        Debug.Log($"🎮 Configuring {controllerType} Player {playerIndex}...");

        // Add SimpleFlexibleInput if not present
        SimpleFlexibleInput flexInput = playerObject.GetComponent<SimpleFlexibleInput>();
        if (flexInput == null)
        {
            flexInput = playerObject.AddComponent<SimpleFlexibleInput>();
            Debug.Log($"  ➕ Added SimpleFlexibleInput component");
        }

        // Configure the flexible input
        flexInput.playerIndex = playerIndex;
        flexInput.enableRealTimeSwitching = enableRealTimeSwitching;
        flexInput.switchCooldown = switchCooldown;

        // Add input injector for advanced integration
        FlexibleInputInjector injector = playerObject.GetComponent<FlexibleInputInjector>();
        if (injector == null)
        {
            injector = playerObject.AddComponent<FlexibleInputInjector>();
            Debug.Log($"  ➕ Added FlexibleInputInjector component");
        }

        injector.playerIndex = playerIndex;
        injector.controllerType = controllerType;

        Debug.Log($"✅ {controllerType} Player {playerIndex} configured for flexible input");
    }

    void VerifySetup()
    {
        // Test player configurations
        SimpleFlexibleInput[] flexInputs = FindObjectsByType<SimpleFlexibleInput>(FindObjectsSortMode.None);
        Debug.Log($"✅ {flexInputs.Length} players configured with flexible input");

        // Test gamepad detection
        Debug.Log($"🎮 {Gamepad.all.Count} gamepads detected:");
        for (int i = 0; i < Gamepad.all.Count; i++)
        {
            Debug.Log($"   Gamepad {i}: {Gamepad.all[i].name}");
        }

        // Test keyboard
        if (Keyboard.current != null)
        {
            Debug.Log("⌨️ Keyboard detected and ready");
        }
        else
        {
            Debug.LogWarning("⚠️ No keyboard detected");
        }
    }

    void OnGUI()
    {
        if (!showDebugUI) return;

        // Setup status
        GUI.Box(new Rect(Screen.width - 320, 10, 300, 100), "Auto Flexible Input Setup");

        GUI.Label(new Rect(Screen.width - 310, 35, 280, 20), $"Setup Complete: {isSetupComplete}");
        GUI.Label(new Rect(Screen.width - 310, 55, 280, 20), $"Players Configured: {playersConfigured}");
        GUI.Label(new Rect(Screen.width - 310, 75, 280, 20), $"Real-time Switching: {enableRealTimeSwitching}");

        // Manual setup button
        if (!isSetupComplete && GUI.Button(new Rect(Screen.width - 310, 85, 120, 20), "Setup Now"))
        {
            SetupEverything();
        }

        // Test instructions
        GUI.Box(new Rect(Screen.width - 320, 120, 300, 80), "Test Instructions");
        GUI.Label(new Rect(Screen.width - 310, 145, 280, 20), "Try switching inputs during play:");
        GUI.Label(new Rect(Screen.width - 310, 165, 280, 20), "Gamepad → WASD → Arrows → Back");
        GUI.Label(new Rect(Screen.width - 310, 180, 280, 20), "Should switch instantly!");
    }

    // Public methods for manual control
    [ContextMenu("Force All Players to Keyboard")]
    public void ForceAllPlayersToKeyboard()
    {
        SimpleFlexibleInput[] flexInputs = FindObjectsByType<SimpleFlexibleInput>(FindObjectsSortMode.None);
        foreach (var flexInput in flexInputs)
        {
            flexInput.currentInputMethod = flexInput.playerIndex == 0 ? "WASD" : "Arrows";
            Debug.Log($"Forced Player {flexInput.playerIndex} to keyboard");
        }
    }

    [ContextMenu("Enable Real-time Switching")]
    public void EnableRealTimeSwitching()
    {
        enableRealTimeSwitching = true;
        SimpleFlexibleInput[] flexInputs = FindObjectsByType<SimpleFlexibleInput>(FindObjectsSortMode.None);
        foreach (var flexInput in flexInputs)
        {
            flexInput.enableRealTimeSwitching = true;
        }
        Debug.Log("Real-time switching enabled for all players");
    }
}
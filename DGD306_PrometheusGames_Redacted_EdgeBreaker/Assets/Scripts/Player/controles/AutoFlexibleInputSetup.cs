// AutoFlexibleInputSetup.cs - Fixed for proper player spawning integration
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

/// <summary>
/// Auto setup for isolated input system - works with your character spawning system
/// Continuously monitors for new players and configures them automatically
/// </summary>
public class AutoFlexibleInputSetup : MonoBehaviour
{
    [Header("Auto Setup Configuration")]
    [Tooltip("Automatically setup when scene starts")]
    public bool autoSetupOnStart = true;

    [Tooltip("Continuously monitor for new players")]
    public bool continuousMonitoring = true;

    [Tooltip("Show debug UI for testing")]
    public bool showDebugUI = true;

    [Tooltip("Check interval for new players (seconds)")]
    public float checkInterval = 0.5f;

    [Header("Status")]
    public bool isSetupComplete = false;
    public int playersConfigured = 0;

    private Coroutine monitoringCoroutine;

    void Start()
    {
        if (autoSetupOnStart)
        {
            StartSetup();
        }
    }

    void StartSetup()
    {
        Debug.Log("🚀 ISOLATED INPUT SETUP STARTING...");

        // Start continuous monitoring for players
        if (continuousMonitoring && monitoringCoroutine == null)
        {
            monitoringCoroutine = StartCoroutine(ContinuousPlayerMonitoring());
        }
    }

    IEnumerator ContinuousPlayerMonitoring()
    {
        while (continuousMonitoring)
        {
            CheckAndSetupNewPlayers();
            yield return new WaitForSeconds(checkInterval);
        }
    }

    void CheckAndSetupNewPlayers()
    {
        // Find all player controllers
        PlayerController[] gunners = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        Player_Melee_Controller1[] meleeControllers = FindObjectsByType<Player_Melee_Controller1>(FindObjectsSortMode.None);

        bool foundNewPlayers = false;

        // Check gunner controllers
        foreach (PlayerController gunner in gunners)
        {
            if (NeedsIsolatedInputSetup(gunner.gameObject))
            {
                SetupPlayerForIsolatedInput(gunner.gameObject, gunner.PlayerIndex, "Gunner");
                foundNewPlayers = true;
            }
        }

        // Check melee controllers
        foreach (Player_Melee_Controller1 melee in meleeControllers)
        {
            if (NeedsIsolatedInputSetup(melee.gameObject))
            {
                SetupPlayerForIsolatedInput(melee.gameObject, melee.PlayerIndex, "Melee");
                foundNewPlayers = true;
            }
        }

        if (foundNewPlayers)
        {
            UpdateStatus();
        }
    }

    bool NeedsIsolatedInputSetup(GameObject playerObject)
    {
        // Check if player already has isolated input components
        SimpleFlexibleInput flexInput = playerObject.GetComponent<SimpleFlexibleInput>();
        FlexibleInputInjector injector = playerObject.GetComponent<FlexibleInputInjector>();

        return flexInput == null || injector == null;
    }

    void SetupPlayerForIsolatedInput(GameObject playerObject, int playerIndex, string controllerType)
    {
        Debug.Log($"🎮 Setting up ISOLATED INPUT for {controllerType} Player {playerIndex}...");

        // Add SimpleFlexibleInput if not present
        SimpleFlexibleInput flexInput = playerObject.GetComponent<SimpleFlexibleInput>();
        if (flexInput == null)
        {
            flexInput = playerObject.AddComponent<SimpleFlexibleInput>();
            Debug.Log($"  ➕ Added SimpleFlexibleInput component");
        }

        // Configure the input
        flexInput.playerIndex = playerIndex;
        flexInput.enableGamepad = true;
        flexInput.enableKeyboard = true;

        // Add input injector if not present
        FlexibleInputInjector injector = playerObject.GetComponent<FlexibleInputInjector>();
        if (injector == null)
        {
            injector = playerObject.AddComponent<FlexibleInputInjector>();
            Debug.Log($"  ➕ Added FlexibleInputInjector component");
        }

        injector.playerIndex = playerIndex;
        injector.controllerType = controllerType;

        Debug.Log($"✅ {controllerType} Player {playerIndex} configured for ISOLATED INPUT");
    }

    void UpdateStatus()
    {
        // Count configured players
        SimpleFlexibleInput[] flexInputs = FindObjectsByType<SimpleFlexibleInput>(FindObjectsSortMode.None);
        playersConfigured = flexInputs.Length;

        if (playersConfigured > 0)
        {
            isSetupComplete = true;
        }

        Debug.Log($"📊 ISOLATED INPUT STATUS: {playersConfigured} players configured");

        // Show player assignments
        foreach (var flexInput in flexInputs)
        {
            Debug.Log($"   Player {flexInput.playerIndex}: {flexInput.currentInputMethod}");
        }
    }

    [ContextMenu("Manual Setup")]
    public void ManualSetup()
    {
        Debug.Log("🔧 MANUAL ISOLATED INPUT SETUP...");
        CheckAndSetupNewPlayers();
        UpdateStatus();
    }

    [ContextMenu("Force Reset All")]
    public void ForceResetAll()
    {
        Debug.Log("🔄 FORCE RESET ALL INPUT...");

        // Release all gamepad assignments
        SimpleFlexibleInput.ReleaseAllGamepadAssignments();

        // Find all players and force setup
        PlayerController[] gunners = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        Player_Melee_Controller1[] meleeControllers = FindObjectsByType<Player_Melee_Controller1>(FindObjectsSortMode.None);

        foreach (PlayerController gunner in gunners)
        {
            // Remove existing components
            SimpleFlexibleInput[] existingFlex = gunner.GetComponents<SimpleFlexibleInput>();
            FlexibleInputInjector[] existingInjectors = gunner.GetComponents<FlexibleInputInjector>();

            foreach (var flex in existingFlex) DestroyImmediate(flex);
            foreach (var injector in existingInjectors) DestroyImmediate(injector);

            // Add fresh components
            SetupPlayerForIsolatedInput(gunner.gameObject, gunner.PlayerIndex, "Gunner");
        }

        foreach (Player_Melee_Controller1 melee in meleeControllers)
        {
            // Remove existing components
            SimpleFlexibleInput[] existingFlex = melee.GetComponents<SimpleFlexibleInput>();
            FlexibleInputInjector[] existingInjectors = melee.GetComponents<FlexibleInputInjector>();

            foreach (var flex in existingFlex) DestroyImmediate(flex);
            foreach (var injector in existingInjectors) DestroyImmediate(injector);

            // Add fresh components
            SetupPlayerForIsolatedInput(melee.gameObject, melee.PlayerIndex, "Melee");
        }

        UpdateStatus();
        Debug.Log("🔄 FORCE RESET COMPLETE!");
    }

    void OnDestroy()
    {
        if (monitoringCoroutine != null)
        {
            StopCoroutine(monitoringCoroutine);
        }
    }

    void OnGUI()
    {
        if (!showDebugUI) return;

        // Status box
        GUI.Box(new Rect(Screen.width - 380, 10, 370, 300), "🎮 ISOLATED INPUT SYSTEM");

        GUI.Label(new Rect(Screen.width - 370, 35, 350, 20), $"Setup Complete: {isSetupComplete}");
        GUI.Label(new Rect(Screen.width - 370, 55, 350, 20), $"Players Configured: {playersConfigured}");
        GUI.Label(new Rect(Screen.width - 370, 75, 350, 20), $"Monitoring: {continuousMonitoring}");
        GUI.Label(new Rect(Screen.width - 370, 95, 350, 20), $"Total Gamepads: {Gamepad.all.Count}");

        // Manual controls
        if (GUI.Button(new Rect(Screen.width - 370, 120, 110, 25), "🔧 Manual Setup"))
        {
            ManualSetup();
        }

        if (GUI.Button(new Rect(Screen.width - 250, 120, 110, 25), "🔄 Force Reset"))
        {
            ForceResetAll();
        }

        if (GUI.Button(new Rect(Screen.width - 130, 120, 110, 25), "📊 Update Status"))
        {
            UpdateStatus();
        }

        // Toggle monitoring
        bool newMonitoring = GUI.Toggle(new Rect(Screen.width - 370, 150, 200, 20), continuousMonitoring, "Continuous Monitoring");
        if (newMonitoring != continuousMonitoring)
        {
            continuousMonitoring = newMonitoring;
            if (continuousMonitoring && monitoringCoroutine == null)
            {
                monitoringCoroutine = StartCoroutine(ContinuousPlayerMonitoring());
            }
            else if (!continuousMonitoring && monitoringCoroutine != null)
            {
                StopCoroutine(monitoringCoroutine);
                monitoringCoroutine = null;
            }
        }

        // Player status
        GUI.Label(new Rect(Screen.width - 370, 180, 350, 20), "Player Status:");

        SimpleFlexibleInput[] flexInputs = FindObjectsByType<SimpleFlexibleInput>(FindObjectsSortMode.None);
        for (int i = 0; i < flexInputs.Length && i < 4; i++)
        {
            var input = flexInputs[i];
            string status = $"P{input.playerIndex}: {input.currentInputMethod}";
            if (input.GetComponent<FlexibleInputInjector>() != null)
            {
                status += " ✅";
            }
            else
            {
                status += " ❌";
            }

            GUI.Label(new Rect(Screen.width - 370, 200 + i * 20, 350, 20), status);
        }

        // Instructions
        GUI.Label(new Rect(Screen.width - 370, 260, 350, 20), "Expected Behavior:");
        GUI.Label(new Rect(Screen.width - 370, 280, 350, 20), "P0: Gamepad0 + WASD | P1: Gamepad1 + Arrows");

        // Emergency mode
        if (playersConfigured == 0 && isSetupComplete == false)
        {
            GUI.color = Color.red;
            if (GUI.Button(new Rect(Screen.width - 370, 290, 350, 25), "🚨 EMERGENCY: No Players Found - Click to Search"))
            {
                ManualSetup();
            }
            GUI.color = Color.white;
        }
    }

    // Public methods for external access
    public void EnableContinuousMonitoring()
    {
        continuousMonitoring = true;
        if (monitoringCoroutine == null)
        {
            monitoringCoroutine = StartCoroutine(ContinuousPlayerMonitoring());
        }
    }

    public void DisableContinuousMonitoring()
    {
        continuousMonitoring = false;
        if (monitoringCoroutine != null)
        {
            StopCoroutine(monitoringCoroutine);
            monitoringCoroutine = null;
        }
    }

    public int GetConfiguredPlayerCount()
    {
        SimpleFlexibleInput[] flexInputs = FindObjectsByType<SimpleFlexibleInput>(FindObjectsSortMode.None);
        return flexInputs.Length;
    }
}
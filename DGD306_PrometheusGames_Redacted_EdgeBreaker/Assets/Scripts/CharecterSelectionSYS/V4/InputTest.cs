using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

public class InputTest : MonoBehaviour
{
    void Update()
    {
        // Test keyboard input
        if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
        {
            Debug.Log("KEYBOARD INPUT DETECTED!");
        }

        // Test gamepad input
        if (Gamepad.current != null)
        {
            if (Gamepad.current.buttonSouth.wasPressedThisFrame ||
                Gamepad.current.buttonNorth.wasPressedThisFrame ||
                Gamepad.current.buttonEast.wasPressedThisFrame ||
                Gamepad.current.buttonWest.wasPressedThisFrame ||
                Gamepad.current.startButton.wasPressedThisFrame)
            {
                Debug.Log("GAMEPAD INPUT DETECTED!");
            }
        }

        // Test specific keys
        if (Keyboard.current != null)
        {
            if (Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                Debug.Log("SPACE KEY PRESSED!");
            }
            if (Keyboard.current.enterKey.wasPressedThisFrame)
            {
                Debug.Log("ENTER KEY PRESSED!");
            }
        }
    }
}
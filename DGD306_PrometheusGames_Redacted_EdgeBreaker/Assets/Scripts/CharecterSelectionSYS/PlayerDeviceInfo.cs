using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Component that stores device assignment info for each player
/// Add this to player prefabs or it will be added automatically by the spawner
/// </summary>
public class PlayerDeviceInfo : MonoBehaviour
{
    [Header("Player Device Assignment")]
    [SerializeField] private int playerIndex = -1;
    [SerializeField] private string deviceName = "None";

    public InputDevice AssignedDevice { get; set; }

    public int PlayerIndex
    {
        get => playerIndex;
        set
        {
            playerIndex = value;
            UpdateDeviceName();
        }
    }

    private void Start()
    {
        UpdateDeviceName();
    }

    private void UpdateDeviceName()
    {
        deviceName = AssignedDevice?.name ?? "None";
    }

    // Debug info
    private void OnValidate()
    {
        UpdateDeviceName();
    }
}
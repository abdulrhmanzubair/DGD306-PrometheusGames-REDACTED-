using UnityEngine;
using UnityEngine.UI;
using TMPro; // Required if using TextMeshPro

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("Ammo Display")]
    [SerializeField] private TMP_Text _secondaryAmmoText; // or use Text for legacy UI
    [SerializeField] private Image _secondaryChargeFill;

    [Header("References")]
    [SerializeField] private GameObject _chargeBarContainer;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
        }
    }

    public void UpdateSecondaryAmmo(int currentAmmo, int maxAmmo = -1)
    {
        if (_secondaryAmmoText != null)
        {
            _secondaryAmmoText.text = maxAmmo > 0 ?
                $"{currentAmmo}/{maxAmmo}" :
                currentAmmo.ToString();
        }
    }

    public void UpdateChargeProgress(float progress)
    {
        if (_secondaryChargeFill != null)
        {
            _secondaryChargeFill.fillAmount = progress;
            _chargeBarContainer.SetActive(progress > 0);
        }
    }

    // Call this when picking up ammo
    public void ShowAmmoPickupNotification(int amount)
    {
        // Implement your notification system here
    }

    // Add other UI update methods as needed
}
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInput))]
public class WeaponSystem : MonoBehaviour
{
    [Header("Primary Fire")]
    [SerializeField] private Transform _firePoint;
    [SerializeField] private GameObject _primaryProjectile;
    [SerializeField] private float _fireRate = 0.2f;
    private float _primaryFireCooldown;

    [Header("Secondary Fire")]
    [SerializeField] private GameObject _secondaryProjectile;
    [SerializeField] private float _secondaryFireCooldown = 1f;
    [SerializeField] private int _secondaryAmmo = 5;
    [SerializeField] private int _maxSecondaryAmmo = 10;
    private float _secondaryFireTimer;
    private bool _isChargingSecondary;

    [Header("Effects")]
    [SerializeField] private ParticleSystem _muzzleFlash;
    [SerializeField] private AudioClip _secondaryFireSound;

    private PlayerInput _playerInput;
    private ObjectPool _projectilePool;

    private void Awake()
    {
        _playerInput = GetComponent<PlayerInput>();
        InitializeObjectPool();
    }

    private void InitializeObjectPool()
    {
        _projectilePool = new ObjectPool(_secondaryProjectile, 10);
    }

    private void Update()
    {
        HandleCooldowns();
        HandleSecondaryCharge();
    }

    private void HandleCooldowns()
    {
        _primaryFireCooldown -= Time.deltaTime;
        _secondaryFireTimer -= Time.deltaTime;
    }

    private void HandleSecondaryCharge()
    {
        if (_isChargingSecondary && _secondaryFireTimer <= 0)
        {
            ReleaseSecondaryFire();
        }
    }

    public void OnPrimaryFire(InputAction.CallbackContext context)
    {
        if (context.performed && _primaryFireCooldown <= 0)
        {
            FirePrimary();
            _primaryFireCooldown = _fireRate;
        }
    }

    public void OnSecondaryFire(InputAction.CallbackContext context)
    {
        if (context.started && _secondaryAmmo > 0)
        {
            StartCharging();
        }
        else if (context.canceled)
        {
            if (_isChargingSecondary) ReleaseSecondaryFire();
        }
    }

    private void StartCharging()
    {
        _isChargingSecondary = true;
        _secondaryFireTimer = _secondaryFireCooldown;
        // Add visual charging effect here
    }

    private void FirePrimary()
    {
        Instantiate(_primaryProjectile, _firePoint.position, _firePoint.rotation);
        _muzzleFlash.Play();
    }

    private void ReleaseSecondaryFire()
    {
        if (_secondaryAmmo <= 0) return;

        GameObject projectile = _projectilePool.GetPooledObject();
        projectile.transform.SetPositionAndRotation(_firePoint.position, _firePoint.rotation);
        projectile.SetActive(true);

        _secondaryAmmo--;
        _isChargingSecondary = false;
        AudioSource.PlayClipAtPoint(_secondaryFireSound, _firePoint.position);

        // Update UI
        UIManager.Instance.UpdateSecondaryAmmo(_secondaryAmmo);
    }

    public void AddSecondaryAmmo(int amount)
    {
        _secondaryAmmo = Mathf.Min(_secondaryAmmo + amount, _maxSecondaryAmmo);
        UIManager.Instance.UpdateSecondaryAmmo(_secondaryAmmo);
    }
}

// Object pooling system
public class ObjectPool
{
    private GameObject _prefab;
    private Queue<GameObject> _pool = new Queue<GameObject>();

    public ObjectPool(GameObject prefab, int initialSize)
    {
        _prefab = prefab;
        for (int i = 0; i < initialSize; i++)
        {
            GameObject obj = GameObject.Instantiate(_prefab);
            obj.SetActive(false);
            _pool.Enqueue(obj);
        }
    }

    public GameObject GetPooledObject()
    {
        if (_pool.Count > 0) return _pool.Dequeue();
        return GameObject.Instantiate(_prefab);
    }

    public void ReturnToPool(GameObject obj)
    {
        obj.SetActive(false);
        _pool.Enqueue(obj);
    }
}
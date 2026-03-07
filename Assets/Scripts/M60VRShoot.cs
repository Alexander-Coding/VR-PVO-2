using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Вешается на объект с XRGrabInteractable (m60 или m60-1).
/// На активацию (нажатие триггера) — вызов выброса гильз и случайный звук выстрела.
/// Поддерживает точку опоры (pivot) для движения оружия относительно неё.
/// </summary>
[RequireComponent(typeof(XRGrabInteractable))]
public class M60VRShoot : MonoBehaviour
{
    [Header("Выстрел и гильзы")]
    [Tooltip("Скрипт выброса гильз (на корне m60 или на этом объекте). Если не задан — ищется на этом объекте или у родителя.")]
    public M60ShellEjection shellEjection;
    [Tooltip("Интервал между выстрелами (сек), чтобы не спамить гильзами")]
    public float fireRate = 0.12f;

    [Header("Звук выстрела")]
    [Tooltip("Клипы выстрела (tush_net_1..4). Если пусто — загружаются из Resources/Sound/ по именам tush_net_1..4.")]
    public AudioClip[] shootClips;
    [Tooltip("Громкость выстрела")]
    [Range(0f, 1f)]
    public float shootVolume = 0.8f;

    [Header("Точка опоры (pivot)")]
    [Tooltip("Точка опоры и захвата (рекомендуется m60 receiver). Мировая позиция не меняется при захвате, если используется GunPivotLockGrabTransformer. Если не задана — ищется дочерний «m60 receiver».")]
    public Transform pivotPoint;
    [Tooltip("Если true, при захвате оружие смещается так, чтобы pivot совпадал с точкой захвата контроллера.")]
    public bool usePivotForGrab = true;

    [Header("Резервный ввод (мышь / клавиатура)")]
    [Tooltip("Стрелять зажатой ЛКМ (мышь) или по триггеру в VR. Для VR привяжите на контроллере Activate к триггеру.")]
    public bool useNonVRFireInput = true;

    [Header("Перегрев")]
    [Tooltip("Случайный интервал стрельбы до перегрева (сек): минимум")]
    public float overheatTimeMin = 30f;
    [Tooltip("Случайный интервал стрельбы до перегрева (сек): максимум")]
    public float overheatTimeMax = 45f;
    [Tooltip("Время остывания после перегрева (сек)")]
    public float cooldownDuration = 5f;

    [Header("Дым из ствола")]
    [Tooltip("Эффект дыма из ствола при выстреле. Если не задан — ищется дочерний MuzzleSmoke.")]
    public ParticleSystem muzzleSmoke;

    [Header("Звук пара при перегреве")]
    [Tooltip("Звук пара/дыма при перегреве. Если не задан — загружается Resources/Sound/steam.")]
    public AudioClip steamSound;

    [Header("Пули")]
    [Tooltip("Префаб или объект пули (m60-bullet). Если не задан — ищется в сцене по имени «m60-bullet».")]
    public GameObject bulletPrefab;
    [Tooltip("Точка вылета пули. Направление полёта задаётся ниже (настраиваемо).")]
    public Transform muzzlePoint;
    [Tooltip("Масштаб пули относительно префаба (реалистично ~0.01–0.1).")]
    [Range(0.001f, 2f)]
    public float bulletScale = 0.05f;
    [Tooltip("Скорость вылета пули (м/с).")]
    public float bulletSpeed = 80f;
    [Tooltip("Через сколько секунд пулю уничтожить (0 — не уничтожать).")]
    public float bulletLifetime = 5f;

    public enum BulletDirectionMode
    {
        [Tooltip("По оси вперёд точки вылета (muzzle forward).")]
        MuzzleForward,
        [Tooltip("По направлению заданного объекта (его forward).")]
        CustomTransform,
        [Tooltip("Направление в локальных осях точки вылета (X=вправо, Y=вверх, Z=вперёд).")]
        LocalDirection
    }

    [Header("Направление полёта пули")]
    [Tooltip("Откуда брать направление: по стволу, от объекта или локальный вектор.")]
    public BulletDirectionMode bulletDirectionMode = BulletDirectionMode.MuzzleForward;
    [Tooltip("Используется при CustomTransform: forward этого объекта = направление пули.")]
    public Transform bulletDirectionTransform;
    [Tooltip("Используется при LocalDirection: направление в локальных осях muzzle (например (0,0,1)=вперёд, (1,0,0)=вправо).")]
    public Vector3 bulletDirectionLocal = new Vector3(0f, 0f, 1f);

    XRGrabInteractable _grab;
    AudioSource _audioSource;
    float _lastFireTime;
    float _fireTimeAccum;
    float _overheatAfter;
    float _cooldownRemaining;
    bool _overheated;
    GameObject _cachedBulletTemplate;
    static readonly string[] TushNetNames = { "tush_net_1", "tush_net_2", "tush_net_3", "tush_net_4" };

    void Awake()
    {
        _grab = GetComponent<XRGrabInteractable>();
        if (shellEjection == null)
        {
            shellEjection = GetComponent<M60ShellEjection>();
            if (shellEjection == null) shellEjection = GetComponentInParent<M60ShellEjection>();
        }
        var receiver = transform.Find("m60 receiver");
        if (receiver != null)
            pivotPoint = receiver;
        if (muzzleSmoke == null)
            muzzleSmoke = transform.Find("MuzzleSmoke")?.GetComponent<ParticleSystem>();
        if (muzzleSmoke == null)
            muzzleSmoke = CreateDefaultMuzzleSmoke();
        if (muzzlePoint == null) muzzlePoint = pivotPoint;
        EnsureBulletTemplate();
        EnsureShootClips();
        EnsureSteamSound();
        ApplyPivotToGrab();
        _overheatAfter = Random.Range(overheatTimeMin, overheatTimeMax);
    }

    void EnsureBulletTemplate()
    {
        if (bulletPrefab != null) { _cachedBulletTemplate = bulletPrefab; return; }
        var found = GameObject.Find("m60-bullet");
        if (found != null) _cachedBulletTemplate = found;
    }

    void EnsureSteamSound()
    {
        if (steamSound != null) return;
        steamSound = Resources.Load<AudioClip>("Sound/steam");
    }

    void ApplyPivotToGrab()
    {
        if (!usePivotForGrab || pivotPoint == null || _grab == null) return;
        _grab.attachTransform = pivotPoint;
    }

    void OnEnable()
    {
        if (_grab != null)
            _grab.activated.AddListener(OnActivated);
    }

    void OnDisable()
    {
        if (_grab != null)
            _grab.activated.RemoveListener(OnActivated);
    }

    void Update()
    {
        if (_overheated)
        {
            _cooldownRemaining -= Time.deltaTime;
            if (_cooldownRemaining <= 0f)
            {
                _overheated = false;
                _overheatAfter = Random.Range(overheatTimeMin, overheatTimeMax);
            }
            return;
        }

        bool fireHeld = GetFireButtonHeld();
        if (useNonVRFireInput && _grab != null && _grab.isSelected && fireHeld)
            DoFire();
    }

    /// <summary>Возвращает true, пока зажата кнопка стрельбы (ЛКМ в новом Input System).</summary>
    bool GetFireButtonHeld()
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null && Mouse.current.leftButton.isPressed)
            return true;
        return false;
#else
        return UnityEngine.Input.GetButton("Fire1") || UnityEngine.Input.GetMouseButton(0);
#endif
    }

    void EnsureShootClips()
    {
        if (shootClips != null && shootClips.Length > 0) return;
        shootClips = new AudioClip[TushNetNames.Length];
        for (int i = 0; i < TushNetNames.Length; i++)
            shootClips[i] = Resources.Load<AudioClip>("Sound/" + TushNetNames[i]);
    }

    void OnActivated(ActivateEventArgs args)
    {
        DoFire();
    }

    void DoFire()
    {
        if (_overheated) return;
        if (Time.time - _lastFireTime < fireRate) return;
        _lastFireTime = Time.time;

        _fireTimeAccum += fireRate;
        if (_fireTimeAccum >= _overheatAfter)
        {
            _overheated = true;
            _cooldownRemaining = cooldownDuration;
            _fireTimeAccum = 0f;
            PlaySteamSound();
            return;
        }

        if (shellEjection != null)
            shellEjection.Fire();

        PlayRandomShootSound();
        if (muzzleSmoke != null)
            muzzleSmoke.Play();
        SpawnBullet();
    }

    void SpawnBullet()
    {
        if (_cachedBulletTemplate == null) return;
        Transform spawn = muzzlePoint != null ? muzzlePoint : transform;
        Vector3 pos = spawn.position;
        Vector3 dir = GetBulletDirection(spawn);
        if (dir.sqrMagnitude < 0.0001f) dir = spawn.forward;
        dir = dir.normalized;
        Quaternion rot = Quaternion.LookRotation(dir);
        GameObject bullet = Instantiate(_cachedBulletTemplate, pos, rot);
        bullet.name = "m60-bullet(Clone)";
        bullet.transform.localScale = Vector3.one * bulletScale;
        Rigidbody rb = bullet.GetComponent<Rigidbody>();
        Vector3 velocity = dir * bulletSpeed;
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.linearVelocity = velocity;
        }
        else
        {
            rb = bullet.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.linearVelocity = velocity;
        }
        if (bulletLifetime > 0f)
            Destroy(bullet, bulletLifetime);
    }

    Vector3 GetBulletDirection(Transform muzzle)
    {
        switch (bulletDirectionMode)
        {
            case BulletDirectionMode.MuzzleForward:
                return muzzle.forward;
            case BulletDirectionMode.CustomTransform:
                if (bulletDirectionTransform != null)
                    return bulletDirectionTransform.forward;
                return muzzle.forward;
            case BulletDirectionMode.LocalDirection:
                return muzzle.TransformDirection(bulletDirectionLocal.normalized);
            default:
                return muzzle.forward;
        }
    }

    void PlayRandomShootSound()
    {
        AudioClip clip = null;
        if (shootClips != null && shootClips.Length > 0)
        {
            int i = Random.Range(0, shootClips.Length);
            if (shootClips[i] != null) clip = shootClips[i];
        }
        if (clip == null)
        {
            EnsureShootClips();
            foreach (var c in shootClips)
                if (c != null) { clip = c; break; }
        }
        if (clip == null) return;

        if (_audioSource == null)
        {
            _audioSource = gameObject.GetComponent<AudioSource>();
            if (_audioSource == null) _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
            _audioSource.spatialBlend = 1f;
        }
        _audioSource.PlayOneShot(clip, shootVolume);
    }

    void PlaySteamSound()
    {
        EnsureSteamSound();
        if (steamSound == null) return;
        if (_audioSource == null)
        {
            _audioSource = gameObject.GetComponent<AudioSource>();
            if (_audioSource == null) _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
            _audioSource.spatialBlend = 1f;
        }
        _audioSource.PlayOneShot(steamSound, 0.8f);
    }

    ParticleSystem CreateDefaultMuzzleSmoke()
    {
        Transform spawnAt = pivotPoint != null ? pivotPoint : transform;
        var go = new GameObject("MuzzleSmoke");
        go.transform.SetParent(spawnAt, false);
        go.transform.localPosition = new Vector3(0f, 0f, 0.5f);
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.duration = 0.2f;
        main.startLifetime = 0.3f;
        main.startSpeed = 2f;
        main.startSize = 0.05f;
        main.maxParticles = 20;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 5) });
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.02f;
        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.gray, 0f), new GradientColorKey(Color.gray, 1f) },
            new[] { new GradientAlphaKey(0.4f, 0f), new GradientAlphaKey(0f, 1f) });
        colorOverLifetime.color = grad;
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        return ps;
    }

    /// <summary>
    /// Точка опоры для захвата: возвращает pivot, если задан, иначе transform.
    /// В Inspector на XRGrabInteractable можно выставить Attach Transform в этот transform (дочерний pivot), чтобы оружие двигалось относительно рукояти.
    /// </summary>
    public Transform GetAttachPivot()
    {
        return pivotPoint != null ? pivotPoint : transform;
    }
}

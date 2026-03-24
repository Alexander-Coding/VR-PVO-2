using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Вешается на объект с XRGrabInteractable (m60 или m60-1).
/// На активацию (триггер VR) или LKM/Пробел — стрельба: выброс гильз, звук, пуля.
/// Поддерживает перегрев, дым из ствола, паровой звук.
/// Если prefab пули не задан и объект "m60-bullet" не найден в сцене — создаётся шаровая пуля.
/// </summary>
[RequireComponent(typeof(XRGrabInteractable))]
public class M60VRShoot : MonoBehaviour
{
    [Header("Выстрел и гильзы")]
    [Tooltip("Скрипт выброса гильз. Если не задан — ищется на этом объекте или у родителя.")]
    public M60ShellEjection shellEjection;
    [Tooltip("Интервал между выстрелами (сек)")]
    public float fireRate = 0.12f;

    [Header("Звук выстрела")]
    [Tooltip("Клипы выстрела (tush_net_1..4). Если пусто — загружаются из Resources/Sound/.")]
    public AudioClip[] shootClips;
    [Range(0f, 1f)]
    public float shootVolume = 0.8f;

    [Header("Точка опоры (pivot)")]
    [Tooltip("Точка опоры (m60 receiver). Если не задана — ищется дочерний 'm60 receiver'.")]
    public Transform pivotPoint;
    [Tooltip("При захвате оружие совмещает pivot с точкой захвата контроллера.")]
    public bool usePivotForGrab = true;

    [Header("Ввод — мышь / клавиатура")]
    [Tooltip("ЛКМ или Пробел (или VR триггер) — стрельба.")]
    public bool useNonVRFireInput = true;

    [Header("Перегрев")]
    public float overheatTimeMin = 10f;
    public float overheatTimeMax = 15f;
    public float cooldownDuration = 15f;
    [Tooltip("Скорость пассивного охлаждения (сек нагрева / сек реального времени). 0 = не охлаждается само.")]
    public float passiveCoolRate = 0.005f;

    [Header("Патроны")]
    [Tooltip("Максимальное количество патронов.")]
    public int maxAmmo = 850;
    [Tooltip("Начальное количество патронов.")]
    public int ammoCount = 100;
    [Tooltip("UI Text для отображения патронов. Если не задан — создаётся автоматически.")]
    public Text ammoText;

    [Header("Дым из ствола")]
    [Tooltip("ParticleSystem дыма. Если не задан — ищется дочерний MuzzleSmoke.")]
    public ParticleSystem muzzleSmoke;

    [Header("Звук пара при перегреве")]
    public AudioClip steamSound;

    [Header("Пули")]
    [Tooltip("Префаб или шаблон пули. Если не задан — ищется 'm60-bullet' в сцене или создаётся шаровая пуля.")]
    public GameObject bulletPrefab;
    [Tooltip("Точка вылета пули (muzzle). Если не задана — используется pivotPoint.")]
    public Transform muzzlePoint;
    [Tooltip("Масштаб пули.")]
    [Range(0.001f, 2f)]
    public float bulletScale = 0.05f;
    [Tooltip("Скорость пули (м/с).")]
    public float bulletSpeed = 80f;
    [Tooltip("Через сколько секунд уничтожить пулю (0 — не уничтожать).")]
    public float bulletLifetime = 5f;

    [Header("След пули")]
    [Tooltip("Показывать трассер (TrailRenderer) за пулей.")]
    public bool showBulletTrail = true;
    [Tooltip("Длина следа в секундах.")]
    public float trailTime = 0.18f;
    [Tooltip("Ширина следа у основания.")]
    public float trailStartWidth = 0.03f;
    [Tooltip("Цвет начала следа.")]
    public Color trailStartColor = new Color(1f, 0.9f, 0.5f, 1f);
    [Tooltip("Цвет конца следа (прозрачный).")]
    public Color trailEndColor = new Color(1f, 0.9f, 0.5f, 0f);

    public enum BulletDirectionMode { MuzzleForward, CustomTransform, LocalDirection }

    [Header("Направление пули")]
    public BulletDirectionMode bulletDirectionMode = BulletDirectionMode.MuzzleForward;
    public Transform bulletDirectionTransform;
    public Vector3 bulletDirectionLocal = new Vector3(0f, 0f, 1f);

    [Header("Конвергенция (сведение стволов)")]
    [Tooltip("Сводить пули к точке прицела. При двух стволах пули сходятся в одну точку.")]
    public bool useConvergence = true;
    [Tooltip("Дистанция схождения (м): на этом расстоянии оба ствола бьют точно в центр прицела.")]
    public float convergenceDistance = 150f;

    XRGrabInteractable _grab;
    AudioSource _audioSource;
    AudioSource _steamSource;
    float _lastFireTime;
    float _fireTimeAccum;
    float _overheatAfter;
    float _cooldownRemaining;
    bool _overheated;
    GameObject _cachedBulletTemplate;
    static readonly string[] TushNetNames = { "tush_net_1", "tush_net_2", "tush_net_3", "tush_net_4" };
    static Text _sharedAmmoText;

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
        // Ищем ствол как точку вылета пули
        if (muzzlePoint == null)
        {
            var barrel = transform.Find("m60 barrel 22in");
            if (barrel != null) muzzlePoint = barrel;
        }
        if (muzzlePoint == null) muzzlePoint = pivotPoint;
        // Пуля летит вдоль локальной оси -X ствола
        bulletDirectionMode = BulletDirectionMode.LocalDirection;
        bulletDirectionLocal = new Vector3(-1f, 0f, 0f);
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
        if (found != null) { _cachedBulletTemplate = found; return; }
        // Создаём шаблон пули из сферы, деактивируем его как шаблон
        _cachedBulletTemplate = CreateDefaultBulletTemplate();
    }

    GameObject CreateDefaultBulletTemplate()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "m60-bullet";
        go.transform.localScale = Vector3.one * 0.05f;
        // Шаблон деактивирован, клонируется при выстреле
        go.SetActive(false);
        // Коллайдер на шаблоне отключаем — включится на клоне при необходимости
        var col = go.GetComponent<Collider>();
        if (col != null) col.enabled = false;
        return go;
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

    void Start()
    {
        if (ammoText == null)
            TryCreateAmmoHUD();
        UpdateAmmoUI();
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
                StopSteamSound();
            }
            return;
        }

        if (useNonVRFireInput && GetFireButtonHeld())
            DoFire();

        // Пассивное охлаждение — нагрев убывает когда не стреляем
        if (passiveCoolRate > 0f && _fireTimeAccum > 0f
            && Time.time - _lastFireTime > fireRate * 2f)
        {
            _fireTimeAccum = Mathf.Max(0f, _fireTimeAccum - passiveCoolRate * Time.deltaTime);
        }
    }

    bool GetFireButtonHeld()
    {
        if (!TurretSeatController.IsMounted) return false;
#if ENABLE_INPUT_SYSTEM
        var mouse = Mouse.current;
        var kb = Keyboard.current;
        if (mouse != null && mouse.leftButton.isPressed) return true;
        if (kb != null && kb.spaceKey.isPressed) return true;
        return false;
#else
        return UnityEngine.Input.GetButton("Fire1")
            || UnityEngine.Input.GetMouseButton(0)
            || UnityEngine.Input.GetKey(KeyCode.Space);
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
        if (!TurretSeatController.IsMounted) return;
        DoFire();
    }

    void DoFire()
    {
        if (_overheated) return;
        if (ammoCount <= 0) return;
        if (Time.time - _lastFireTime < fireRate) return;
        _lastFireTime = Time.time;

        ammoCount--;
        UpdateAmmoUI();

        _fireTimeAccum += fireRate;
        if (_fireTimeAccum >= _overheatAfter)
        {
            _overheated = true;
            _cooldownRemaining = cooldownDuration;
            _fireTimeAccum = 0f;
            PlaySteamSound();
            return;
        }

        if (shellEjection != null) shellEjection.Fire();
        PlayRandomShootSound();
        if (muzzleSmoke != null) muzzleSmoke.Play();
        SpawnBullet();
    }

    void SpawnBullet()
    {
        if (_cachedBulletTemplate == null) return;

        Transform spawn = muzzlePoint != null ? muzzlePoint : (pivotPoint != null ? pivotPoint : transform);
        Vector3 barrelDir = GetBulletDirection(spawn);
        if (barrelDir.sqrMagnitude < 0.0001f) barrelDir = spawn.forward;
        barrelDir = barrelDir.normalized;

        // Смещаем точку спавна на 0.6 м вперёд — ствол может быть в казённой части,
        // и пуля иначе появится внутри коллайдеров самой пушки.
        Vector3 pos = spawn.position + barrelDir * 0.6f;

        Vector3 dir;
        if (useConvergence && convergenceDistance > 0f)
        {
            // Точка фокуса — куда указывает ствол на дистанции схождения.
            // Оба ствола (m60 и m60-1) целятся в одну мировую точку,
            // поэтому трассы сходятся ровно в прицеле.
            Vector3 focusPoint = spawn.position + barrelDir * convergenceDistance;
            dir = (focusPoint - pos).normalized;
        }
        else
        {
            dir = barrelDir;
        }

        Quaternion rot = Quaternion.LookRotation(dir);

        GameObject bullet = Instantiate(_cachedBulletTemplate, pos, rot);
        bullet.name = "m60-bullet(Clone)";
        bullet.transform.localScale = Vector3.one * bulletScale;
        bullet.SetActive(true);

        // Гарантируем коллайдер (нужен для попадания)
        var existingCol = bullet.GetComponent<Collider>();
        if (existingCol != null)
            existingCol.enabled = true;
        else
        {
            var sc = bullet.AddComponent<SphereCollider>();
            sc.radius = 0.5f;
        }

        // Увеличенный trigger — зона зачёта попадания (~0.3 м при bulletScale=0.05)
        var hitTrigger = bullet.AddComponent<SphereCollider>();
        hitTrigger.isTrigger = true;
        hitTrigger.radius = 6.0f;

        Rigidbody rb = bullet.GetComponent<Rigidbody>();
        if (rb == null) rb = bullet.AddComponent<Rigidbody>();
        rb.isKinematic = false;
        rb.useGravity = false;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.linearVelocity = dir * bulletSpeed;

        // Игнорируем коллизии со всей иерархией пушки (дочерние И родительские объекты),
        // иначе пуля мгновенно уничтожается, задев коллайдер платформы или лафета.
        var bulletColliders = bullet.GetComponents<Collider>();
        var seen = new System.Collections.Generic.HashSet<Collider>();
        foreach (var gc in GetComponentsInChildren<Collider>(true)) seen.Add(gc);
        foreach (var gc in GetComponentsInParent<Collider>(true))  seen.Add(gc);
        foreach (var gc in seen)
            foreach (var bc in bulletColliders)
                Physics.IgnoreCollision(bc, gc, true);

        if (showBulletTrail)
        {
            var trail = bullet.AddComponent<TrailRenderer>();
            trail.time          = trailTime;
            trail.startWidth    = trailStartWidth;
            trail.endWidth      = 0f;
            trail.startColor    = trailStartColor;
            trail.endColor      = trailEndColor;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;
            // Стандартный Sprites/Default материал — виден без дополнительных ассетов
            trail.material = new Material(Shader.Find("Sprites/Default"));
        }

        if (bullet.GetComponent<BulletHit>() == null)
            bullet.AddComponent<BulletHit>();

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
                return bulletDirectionTransform != null ? bulletDirectionTransform.forward : muzzle.forward;
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
            foreach (var c in shootClips) if (c != null) { clip = c; break; }
        }
        if (clip == null) return;

        EnsureAudioSource();
        _audioSource.PlayOneShot(clip, shootVolume);
    }

    void PlaySteamSound()
    {
        EnsureSteamSound();
        if (steamSound == null) return;
        if (_steamSource == null)
        {
            _steamSource = gameObject.AddComponent<AudioSource>();
            _steamSource.playOnAwake = false;
            _steamSource.spatialBlend = 1f;
            _steamSource.loop = true;
        }
        _steamSource.clip = steamSound;
        _steamSource.volume = 0.8f;
        _steamSource.Play();
    }

    void StopSteamSound()
    {
        if (_steamSource != null && _steamSource.isPlaying)
            _steamSource.Stop();
    }

    void EnsureAudioSource()
    {
        if (_audioSource != null) return;
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
            _audioSource.spatialBlend = 1f;
        }
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
        // Stop immediately — Unity starts playing the PS on AddComponent; duration can't be
        // changed while the system is playing.
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
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
    /// Добавляет патроны (вызывается AmmoPickupZone при загрузке ящика).
    /// </summary>
    public void AddAmmo(int amount)
    {
        ammoCount = Mathf.Min(ammoCount + amount, maxAmmo);
        UpdateAmmoUI();
    }

    void UpdateAmmoUI()
    {
        if (ammoText != null)
            ammoText.text = $"Патроны: {ammoCount} / {maxAmmo}";
    }

    void TryCreateAmmoHUD()
    {
        // Используем уже созданный текст другим экземпляром (два ствола — один HUD)
        if (_sharedAmmoText != null && _sharedAmmoText)
        {
            ammoText = _sharedAmmoText;
            return;
        }

        var canvasGO = new GameObject("AmmoHUD");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>();

        var textGO = new GameObject("Text");
        textGO.transform.SetParent(canvasGO.transform, false);

        var textRT = textGO.AddComponent<RectTransform>();
        // Нижний левый угол экрана
        textRT.anchorMin = new Vector2(0f, 0f);
        textRT.anchorMax = new Vector2(0.3f, 0.08f);
        textRT.offsetMin = new Vector2(16f, 12f);
        textRT.offsetMax = new Vector2(-4f, -4f);

        var t = textGO.AddComponent<Text>();
        t.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize  = 32;
        t.color     = new Color(1f, 0.9f, 0.2f, 1f);
        t.alignment = TextAnchor.MiddleLeft;

        ammoText        = t;
        _sharedAmmoText = t;
    }

    public Transform GetAttachPivot() => pivotPoint != null ? pivotPoint : transform;

    /// <summary>
    /// Доля нагрева 0..1. Во время охлаждения убывает от 1 до 0.
    /// </summary>
    public float HeatFraction
    {
        get
        {
            if (_overheated)
                return cooldownDuration > 0f ? Mathf.Clamp01(_cooldownRemaining / cooldownDuration) : 1f;
            if (_overheatAfter <= 0f) return 0f;
            return Mathf.Clamp01(_fireTimeAccum / _overheatAfter);
        }
    }

    public bool IsOverheated => _overheated;
}

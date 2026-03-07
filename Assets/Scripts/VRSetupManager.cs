using UnityEngine;

/// <summary>
/// Ставит XR Origin (персонажа) на объект GunPlatform при старте и проигрывает вступительный звук.
/// Требования: в сцене есть объект с именем "GunPlatform" и "XR Origin".
/// </summary>
public class VRSetupManager : MonoBehaviour
{
    [Header("Стартовое положение")]
    [Tooltip("Включить перемещение персонажа на GunPlatform при старте. Если выключено — персонаж остаётся там, где стоит в сцене.")]
    public bool movePlayerOnSpawn = false;
    [Tooltip("Точка появления (пустой объект или платформа). Если не задан — ищется по имени \"GunPlatform\".")]
    public Transform gunPlatform;
    [Tooltip("Дополнительный поворот спавна относительно платформы (X, Y, Z в градусах). Ось Y = поворот «куда смотрите» (0…360). X и Z не трогайте, если не нужен наклон — иначе можно перевернуться.")]
    public Vector3 spawnRotationOffset = new Vector3(0f, 180f, 0f);

    [Header("Стартовый звук")]
    [Tooltip("Звук при запуске игры (например, Good Morning Vietnam). Если не задан — загружается из Resources или по пути.")]
    public AudioClip startupClip;
    [Tooltip("Путь к клипу в Assets, если не назначен в Inspector (например: Sound/Good_Morning_Vietnam_budilnik)")]
    public string startupClipPath = "Sound/Good_Morning_Vietnam_budilnik";
    [Tooltip("Громкость стартового звука")]
    [Range(0f, 1f)]
    public float startupVolume = 0.7f;

    const string XrOriginName = "XR Origin";
    const string GunPlatformName = "GunPlatform";

    void Start()
    {
        PlacePlayerOnGunPlatform();
        PlayStartupSound();
    }

    void PlacePlayerOnGunPlatform()
    {
        if (!movePlayerOnSpawn) return;

        Transform platform = gunPlatform != null ? gunPlatform : FindTransformByName(GunPlatformName);
        if (platform == null)
        {
            Debug.LogWarning("[VRSetupManager] Объект GunPlatform не найден. Создайте пустой объект с именем \"GunPlatform\" и поставьте его там, где должен стоять игрок.");
            return;
        }

        Transform xrOrigin = FindXROrigin();
        if (xrOrigin == null)
        {
            Debug.LogWarning("[VRSetupManager] XR Origin не найден в сцене.");
            return;
        }

        // Трекинг VR перезаписывает transform XR Origin каждый кадр, поэтому поворот/позиция задаются родительскому ригу, а не самому XR Origin.
        Transform originalParent = xrOrigin.parent;
        GameObject rigGo = new GameObject("PlayerSpawnRig");
        Transform rig = rigGo.transform;
        rig.position = platform.position;
        rig.rotation = platform.rotation * Quaternion.Euler(spawnRotationOffset);
        if (originalParent != null)
            rig.SetParent(originalParent);
        rig.localScale = Vector3.one;

        xrOrigin.SetParent(rig);
        xrOrigin.localPosition = Vector3.zero;
        xrOrigin.localRotation = Quaternion.identity;
        xrOrigin.localScale = Vector3.one;

        Debug.Log("[VRSetupManager] Персонаж поставлен на GunPlatform (риг PlayerSpawnRig).");
    }

    Transform FindXROrigin()
    {
        var go = GameObject.Find(XrOriginName);
        return go != null ? go.transform : null;
    }

    Transform FindTransformByName(string name)
    {
        var all = FindObjectsByType<Transform>(FindObjectsSortMode.None);
        foreach (var t in all)
            if (t.name == name) return t;
        return null;
    }

    void PlayStartupSound()
    {
        AudioClip clip = startupClip;
        if (clip == null && !string.IsNullOrEmpty(startupClipPath))
        {
            // Пробуем загрузить из Resources (положите клип в Assets/Resources/Sound/)
            clip = Resources.Load<AudioClip>(startupClipPath);
            if (clip == null)
            {
                Debug.LogWarning("[VRSetupManager] Стартовый звук не найден. Назначьте AudioClip в Inspector или положите клип в Resources: " + startupClipPath);
                return;
            }
        }

        if (clip == null) return;

        var audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.clip = clip;
        audioSource.volume = startupVolume;
        audioSource.spatialBlend = 0f;
        audioSource.Play();
    }
}

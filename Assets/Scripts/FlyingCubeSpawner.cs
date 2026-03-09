using UnityEngine;

/// <summary>
/// Спавнит летающие цели в позиции спавнера: либо случайный префаб из vehiclePrefabs (самолёты/вертолёты),
/// либо при отсутствии префабов — куб. Цели взлетают и кружат по маршруту (восьмёрка/овал).
/// Максимум maxCubes; у каждого своя высота и фаза, чтобы не сталкиваться.
/// </summary>
public class FlyingCubeSpawner : MonoBehaviour
{
    const string DecorationObjectName = "DecorationObject";
    const string PlayerPlatformName = "GunPlatform";

    [Header("Спавн")]
    [Tooltip("Префабы летающих целей (например A10, AH64, B2, F35, Sr71, Su57 из Low Poly Military Vehicles). Если пусто — спавнятся кубы.")]
    public GameObject[] vehiclePrefabs;
    [Tooltip("Интервал проверки появления цели (сек)")]
    public float spawnInterval = 4f;
    [Tooltip("Максимум целей в полёте")]
    public int maxCubes = 5;
    [Tooltip("Масштаб цели (куба или префаба)")]
    public Vector3 cubeScale = Vector3.one * 0.25f;
    [Tooltip("Материал для летающих целей (например Palet с текстурой MilSim). Если задан — применяется ко всем рендерерам префаба.")]
    public Material vehicleMaterial;
    [Tooltip("Центр орбиты (над какими объектами кружить). Если не задан — берётся DecorationObject.")]
    public Transform orbitCenterOverride;
    [Header("Сброс бомб")]
    [Tooltip("Префабы сбрасываемых бомб (BTM_Rockets_Missiles_Bombs). Если пусто — сбрасываются кубы.")]
    public GameObject[] droppedBombPrefabs;
    [Tooltip("Префаб эффекта взрыва при ударе о землю (Cartoon explosion BOOM).")]
    public GameObject explosionEffectPrefab;
    [Tooltip("Зона над платформой игрока: сюда бомбы не сбрасываются. Центр зоны (если не задан — ищется GunPlatform).")]
    public Transform playerPlatformCenter;
    [Tooltip("Радиус зоны «не сбрасывать» вокруг платформы игрока (XZ)")]
    public float noDropRadius = 7f;
    [Tooltip("Звук взрыва при падении бомбы на землю. Можно положить в Resources/ExplosionSound.")]
    public AudioClip explosionSound;

    float _nextSpawnTime;
    Transform _decoration;

    void Start()
    {
        _decoration = FindTransformByName(DecorationObjectName);
        if (_decoration == null)
            Debug.LogWarning("[FlyingCubeSpawner] Объект DecorationObject не найден в сцене.");
        _nextSpawnTime = Time.time;
    }

    void Update()
    {
        Transform center = orbitCenterOverride != null ? orbitCenterOverride : _decoration;
        if (center == null) return;
        if (Time.time < _nextSpawnTime) return;
        if (CountFlyingCubes() >= maxCubes) return;

        _nextSpawnTime = Time.time + spawnInterval;

        Vector3 spawnPos = transform.position;
        GameObject cube;
        if (vehiclePrefabs != null && vehiclePrefabs.Length > 0)
        {
            GameObject prefab = vehiclePrefabs[Random.Range(0, vehiclePrefabs.Length)];
            if (prefab == null) return;
            cube = Instantiate(prefab);
            cube.name = "FlyingVehicle_" + prefab.name;
            var rb = cube.GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = true;
            if (vehicleMaterial != null)
            {
                foreach (var r in cube.GetComponentsInChildren<Renderer>(true))
                    r.material = vehicleMaterial;
            }
        }
        else
        {
            cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "FlyingCube";
        }
        cube.transform.position = spawnPos;
        cube.transform.localScale = cubeScale;

        var movement = cube.GetComponent<FlyingCubeMovement>();
        if (movement == null) movement = cube.AddComponent<FlyingCubeMovement>();
        int n = CountFlyingCubes();
        movement.pathType = (FlyingCubeMovement.PathType)(n % 3);
        float step = 360f / Mathf.Max(1, maxCubes);
        float startAngle = (n * step) % 360f + Random.Range(0f, step * 0.25f);
        float heightOffset = (n % 4) * 5f + Random.Range(0f, 1.2f);
        float baseScale = 0.6f + (n % 4) * 0.45f + Random.Range(0f, 0.08f);
        float scaleX = baseScale * (0.85f + (n % 3) * 0.15f);
        float scaleZ = baseScale * (0.85f + ((n + 1) % 3) * 0.15f);
        float speedMultiplier = 0.75f + (n % 5) * 0.1f + Random.Range(0f, 0.06f);
        Transform platform = playerPlatformCenter != null ? playerPlatformCenter : FindTransformByName(PlayerPlatformName);
        Vector3 noDropCenter = platform != null ? platform.position : center.position;
        movement.SetOrbit(center.position, spawnPos, startAngle, heightOffset, scaleX, scaleZ, speedMultiplier, noDropCenter, noDropRadius, explosionSound, droppedBombPrefabs, explosionEffectPrefab);
    }

    static int CountFlyingCubes()
    {
        return Object.FindObjectsByType<FlyingCubeMovement>(FindObjectsSortMode.None).Length;
    }

    static Transform FindTransformByName(string name)
    {
        var go = GameObject.Find(name);
        return go != null ? go.transform : null;
    }
}

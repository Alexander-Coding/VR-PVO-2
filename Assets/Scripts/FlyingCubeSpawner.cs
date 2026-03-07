using UnityEngine;

/// <summary>
/// Спавнит кубы в позиции спавнера (поставь объект в стороне от декораций).
/// Куб взлетает с земли и кружит над объектами по маршруту (восьмёрка/овал).
/// Максимум maxCubes; у каждого своя высота и фаза, чтобы не сталкиваться.
/// </summary>
public class FlyingCubeSpawner : MonoBehaviour
{
    const string DecorationObjectName = "DecorationObject";
    const string PlayerPlatformName = "GunPlatform";

    [Header("Спавн")]
    [Tooltip("Интервал проверки появления куба (сек)")]
    public float spawnInterval = 4f;
    [Tooltip("Максимум кубов в полёте")]
    public int maxCubes = 5;
    [Tooltip("Масштаб куба")]
    public Vector3 cubeScale = Vector3.one * 0.5f;
    [Tooltip("Центр орбиты (над какими объектами кружить). Если не задан — берётся DecorationObject.")]
    public Transform orbitCenterOverride;
    [Header("Сброс маленьких кубиков")]
    [Tooltip("Зона над платформой игрока: сюда маленькие кубики не падают. Центр зоны (если не задан — ищется GunPlatform).")]
    public Transform playerPlatformCenter;
    [Tooltip("Радиус зоны «не сбрасывать» вокруг платформы игрока (XZ)")]
    public float noDropRadius = 7f;
    [Tooltip("Звук взрыва при падении маленького кубика на землю. Можно положить в Resources/ExplosionSound.")]
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

        // Появление на земле в позиции спавнера (перенеси объект FlyingCubeSpawner в сторону)
        Vector3 spawnPos = transform.position;
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = "FlyingCube";
        cube.transform.position = spawnPos;
        cube.transform.localScale = cubeScale;

        var movement = cube.AddComponent<FlyingCubeMovement>();
        movement.pathType = FlyingCubeMovement.PathType.Figure8;
        float startAngle = Random.Range(0f, 360f);
        float heightOffset = Random.Range(0f, 4f);
        float radiusScale = Random.Range(0.88f, 1.25f);
        float speedMultiplier = Random.Range(0.75f, 1.2f);
        Transform platform = playerPlatformCenter != null ? playerPlatformCenter : FindTransformByName(PlayerPlatformName);
        Vector3 noDropCenter = platform != null ? platform.position : center.position;
        movement.SetOrbit(center.position, spawnPos, startAngle, heightOffset, radiusScale, speedMultiplier, noDropCenter, noDropRadius, explosionSound);
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

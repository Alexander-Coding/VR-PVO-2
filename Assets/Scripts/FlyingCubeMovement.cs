using UnityEngine;

/// <summary>
/// Взлёт с земли, затем полёт по траектории (круг/овал/восьмёрка) вокруг базы, как самолёт.
/// Разные кубы получают смещение по высоте и фазе, чтобы не сталкиваться.
/// </summary>
public class FlyingCubeMovement : MonoBehaviour
{
    public enum PathType { Circle, Oval, Figure8 }

    [Header("Взлёт")]
    [Tooltip("Время набора высоты от земли до маршрута (сек)")]
    public float climbDuration = 2.5f;

    [Header("Маршрут")]
    [Tooltip("Тип траектории")]
    public PathType pathType = PathType.Figure8;
    [Tooltip("Радиус/полуось по X")]
    public float radiusX = 12f;
    [Tooltip("Радиус/полуось по Z (для овала и восьмёрки)")]
    public float radiusZ = 8f;
    [Tooltip("Высота полёта над базой")]
    public float orbitHeight = 22f;
    [Tooltip("Размах изменения высоты в полёте (плавный), в метрах")]
    public float heightVariationAmplitude = 0.5f;
    [Tooltip("Скорость изменения высоты (циклов в секунду) — чем меньше, тем плавнее")]
    public float heightVariationSpeed = 0.18f;
    [Tooltip("Базовая скорость облёта (градусов в секунду)")]
    public float angularSpeedDeg = 38f;
    [Tooltip("Крен в вираже (градусы)")]
    public float bankAngle = 18f;

    [Header("Сброс маленького кубика (редко)")]
    [Tooltip("Вероятность сброса за одну проверку (проверка раз в ~2 сек)")]
    [Range(0f, 1f)]
    public float dropChancePerCheck = 0.12f;
    [Tooltip("Интервал проверки «сбросить или нет» (сек)")]
    public float dropCheckInterval = 2.2f;
    [Tooltip("Масштаб сбрасываемой бомбы/кубика")]
    public float droppedCubeScale = 0.16f;

    Vector3 _baseCenter;
    Vector3 _noDropCenter;   // над этой зоной (платформа игрока) не сбрасываем
    float _noDropRadius;
    AudioClip _explosionClip;
    GameObject[] _droppedBombPrefabs;
    GameObject _explosionEffectPrefab;
    float _heightPhase;
    float _angleDeg;
    float _heightOffset;
    float _radiusScale = 1f;
    float _radiusScaleZ = 1f;   // отдельный масштаб по Z — разная форма траектории
    float _speedMultiplier = 1f;
    bool _initialized;
    bool _climbing;
    float _climbTimer;
    Vector3 _spawnPosition;
    Vector3 _orbitStartPosition;
    float _nextDropCheckTime;

    /// <summary>
    /// Задать орбиту и точку взлёта. Вызывается спавнером.
    /// </summary>
    /// <param name="baseCenter">Центр области (над объектами)</param>
    /// <param name="spawnPosition">Точка на земле, откуда взлетает куб</param>
    /// <param name="startAngleDeg">Начальный угол на маршруте</param>
    /// <param name="heightOffset">Смещение по высоте относительно других кубов</param>
    /// <param name="radiusScale">Масштаб радиуса по X (размер и форма траектории)</param>
    /// <param name="radiusScaleZ">Масштаб по Z (если &lt; 0 — равен radiusScale)</param>
    /// <param name="speedMultiplier">Множитель скорости (кубы летят не синхронно)</param>
    /// <param name="noDropCenter">Центр зоны, над которой не сбрасывать кубики (платформа игрока)</param>
    /// <param name="noDropRadius">Радиус этой зоны (XZ)</param>
    /// <param name="explosionClip">Звук взрыва при падении</param>
    /// <param name="droppedBombPrefabs">Префабы бомб для сброса (если null — сбрасывается куб)</param>
    /// <param name="explosionEffectPrefab">Префаб эффекта взрыва при ударе о землю</param>
    public void SetOrbit(Vector3 baseCenter, Vector3 spawnPosition, float startAngleDeg, float heightOffset = 0f, float radiusScale = 1f, float radiusScaleZ = -1f, float speedMultiplier = 1f, Vector3? noDropCenter = null, float noDropRadius = 6f, AudioClip explosionClip = null, GameObject[] droppedBombPrefabs = null, GameObject explosionEffectPrefab = null)
    {
        _baseCenter = baseCenter;
        _spawnPosition = spawnPosition;
        _angleDeg = startAngleDeg;
        _heightOffset = heightOffset;
        _radiusScale = radiusScale;
        _radiusScaleZ = radiusScaleZ >= 0f ? radiusScaleZ : radiusScale;
        _speedMultiplier = speedMultiplier;
        _noDropCenter = noDropCenter ?? baseCenter;
        _noDropRadius = noDropRadius;
        _explosionClip = explosionClip;
        _droppedBombPrefabs = droppedBombPrefabs;
        _explosionEffectPrefab = explosionEffectPrefab;
        _heightPhase = Random.Range(0f, 6.28f);
        _climbing = true;
        _climbTimer = 0f;
        _nextDropCheckTime = Time.time + dropCheckInterval;
        _initialized = true;

        transform.position = _spawnPosition;
        _orbitStartPosition = GetPathPosition(0f);
    }

    void Update()
    {
        if (!_initialized) return;

        if (_climbing)
        {
            _climbTimer += Time.deltaTime;
            float t = Mathf.Clamp01(_climbTimer / climbDuration);
            t = t * t * (3f - 2f * t);
            Vector3 targetPos = GetPathPosition(_angleDeg * Mathf.Deg2Rad);
            transform.position = Vector3.Lerp(_spawnPosition, targetPos, t);
            transform.rotation = Quaternion.Slerp(
                Quaternion.LookRotation(Vector3.forward, Vector3.up),
                GetPathRotation(_angleDeg * Mathf.Deg2Rad),
                t);
            if (_climbTimer >= climbDuration)
                _climbing = false;
            return;
        }

        _angleDeg += (angularSpeedDeg * _speedMultiplier) * Time.deltaTime;
        if (_angleDeg >= 360f) _angleDeg -= 360f;
        if (_angleDeg < 0f) _angleDeg += 360f;

        float rad = _angleDeg * Mathf.Deg2Rad;
        transform.position = GetPathPosition(rad);
        transform.rotation = GetPathRotation(rad);

        TryDropSmallCube();
    }

    void TryDropSmallCube()
    {
        if (Time.time < _nextDropCheckTime) return;
        _nextDropCheckTime = Time.time + dropCheckInterval;
        if (Random.value > dropChancePerCheck) return;

        Vector3 pos = transform.position;
        float dx = pos.x - _noDropCenter.x;
        float dz = pos.z - _noDropCenter.z;
        if (dx * dx + dz * dz < _noDropRadius * _noDropRadius) return;

        GameObject small;
        if (_droppedBombPrefabs != null && _droppedBombPrefabs.Length > 0)
        {
            GameObject prefab = _droppedBombPrefabs[Random.Range(0, _droppedBombPrefabs.Length)];
            if (prefab == null) return;
            small = Instantiate(prefab);
            small.name = "DroppedBomb";
        }
        else
        {
            small = GameObject.CreatePrimitive(PrimitiveType.Cube);
            small.name = "DroppedCube";
        }
        small.transform.position = pos;
        small.transform.rotation = Quaternion.Euler(180f, 0f, 0f);
        small.transform.localScale = Vector3.one * droppedCubeScale;
        if (small.GetComponentInChildren<Collider>(true) == null)
        {
            var col = small.AddComponent<SphereCollider>();
            col.radius = 0.5f;
        }
        var rb = small.GetComponent<Rigidbody>();
        if (rb == null) rb = small.AddComponent<Rigidbody>();
        rb.useGravity = true;
        rb.isKinematic = false;
        rb.mass = 0.5f;
        var explosion = small.GetComponent<DroppedCubeExplosion>();
        if (explosion == null) explosion = small.AddComponent<DroppedCubeExplosion>();
        explosion.explosionClip = _explosionClip;
        explosion.explosionEffectPrefab = _explosionEffectPrefab;
    }

    Vector3 GetPathPosition(float angleRad)
    {
        float x = _baseCenter.x;
        float z = _baseCenter.z;
        // Плавное изменение высоты (не резкое)
        float heightVariation = heightVariationAmplitude * Mathf.Sin((Time.time * heightVariationSpeed) * Mathf.PI * 2f + _heightPhase);
        float y = _baseCenter.y + orbitHeight + _heightOffset + heightVariation;

        float sx = radiusX * _radiusScale;
        float sz = radiusZ * _radiusScaleZ;
        switch (pathType)
        {
            case PathType.Circle:
                float r = (sx + sz) * 0.5f;
                x += r * Mathf.Cos(angleRad);
                z += r * Mathf.Sin(angleRad);
                break;
            case PathType.Oval:
                x += sx * Mathf.Cos(angleRad);
                z += sz * Mathf.Sin(angleRad);
                break;
            case PathType.Figure8:
                x += sx * Mathf.Cos(angleRad);
                z += sz * Mathf.Sin(2f * angleRad);
                break;
            default:
                x += sx * Mathf.Cos(angleRad);
                z += sz * Mathf.Sin(angleRad);
                break;
        }

        return new Vector3(x, y, z);
    }

    Quaternion GetPathRotation(float angleRad)
    {
        float delta = 0.01f;
        Vector3 pos = GetPathPosition(angleRad);
        Vector3 nextPos = GetPathPosition(angleRad + delta);
        Vector3 tangent = (nextPos - pos).normalized;
        tangent.y = 0f;
        if (tangent.sqrMagnitude < 0.01f) tangent = Vector3.forward;
        tangent.Normalize();
        Vector3 up = Quaternion.AngleAxis(-bankAngle, tangent) * Vector3.up;
        return Quaternion.LookRotation(tangent, up);
    }
}

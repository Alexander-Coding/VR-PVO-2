using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(FlyingCubeSpawner))]
public class FlyingCubeSpawnerEditor : Editor
{
    static readonly string[] AircraftPrefabNames = { "A10", "AH64", "B2", "F35", "Sr71", "Su57" };
    const string PrefabsPath = "Assets/LowPolyMiliteryVehicles/Prefabs";

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        if (GUILayout.Button("Подставить префабы самолётов (A10, AH64, B2, F35, Sr71, Su57)"))
        {
            var spawner = (FlyingCubeSpawner)target;
            var prefabs = new GameObject[AircraftPrefabNames.Length];
            for (int i = 0; i < AircraftPrefabNames.Length; i++)
            {
                string path = $"{PrefabsPath}/{AircraftPrefabNames[i]}.prefab";
                prefabs[i] = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefabs[i] == null)
                    Debug.LogWarning($"[FlyingCubeSpawner] Префаб не найден: {path}");
            }
            spawner.vehiclePrefabs = prefabs;
            var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/LowPolyMiliteryVehicles/Materials/Palet.mat");
            if (mat != null) spawner.vehicleMaterial = mat;
            EditorUtility.SetDirty(spawner);
        }

        if (GUILayout.Button("Подставить префабы бомб и эффект взрыва (BTM + Cartoon BOOM)"))
        {
            var spawner = (FlyingCubeSpawner)target;
            string[] bombNames = { "RMB_02", "RMB_03", "RMB_06", "RMB_08", "RMB_12", "RMB_14" };
            string bombFolder = "Assets/BTM_Assets/BTM_Rockets_Missiles_Bombs/Prefabs/Grey";
            var bombs = new GameObject[bombNames.Length];
            for (int i = 0; i < bombNames.Length; i++)
            {
                string path = $"{bombFolder}/{bombNames[i]}.prefab";
                bombs[i] = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (bombs[i] == null) Debug.LogWarning($"[FlyingCubeSpawner] Бомба не найдена: {path}");
            }
            spawner.droppedBombPrefabs = bombs;
            var boom = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/eretichable Technologies/Cartoon explosion BOOM Weapons Gun VFX/Prefab/Boom.prefab");
            if (boom != null) spawner.explosionEffectPrefab = boom;
            else Debug.LogWarning("[FlyingCubeSpawner] Префаб Boom не найден.");
            EditorUtility.SetDirty(spawner);
        }
    }
}

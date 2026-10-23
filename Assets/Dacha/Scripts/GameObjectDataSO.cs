using UnityEngine;

// Per-level data. Create one per level (Create > Dacha > Level Data) inside a
// "Resources/LevelData" folder and name it after the scene (e.g. "Level1").
// The exit door in that scene finds it automatically.
[CreateAssetMenu(menuName = "Dacha/Level Data", fileName = "Level1")]
public class GameObjectDataSO : ScriptableObject
{
    [Tooltip("Scene this data is for. Leave empty to use this asset's name.")]
    [SerializeField] string sceneName;

    [Header("Exit Door")]
    [Tooltip("Sensor IDs that must be ON to open the door, in the order shown on the door sign. " +
             "Set the same IDs in each sensor's SensorController > Sensor Id.")]
    public string[] requiredSensorIds = { "Sensor_A", "Sensor_B", "Sensor_C" };
    [Tooltip("Seconds to wait after the last sensor turns on before the door opens.")]
    public float doorOpenDelay = 1.5f;
    [Tooltip("If off, the door closes again when a sensor is switched back off.")]
    public bool doorStaysOpen = true;

    public string SceneName => string.IsNullOrEmpty(sceneName) ? name : sceneName;

    const string ResourcesFolder = "LevelData";

    // Finds the data for a scene among all assets in Resources/LevelData.
    public static GameObjectDataSO ForScene(string scene)
    {
        foreach (var data in Resources.LoadAll<GameObjectDataSO>(ResourcesFolder))
            if (data.SceneName == scene) return data;
        return null;
    }
}

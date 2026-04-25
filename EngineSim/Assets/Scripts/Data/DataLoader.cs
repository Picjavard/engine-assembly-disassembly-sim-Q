using System.Collections.Generic;
using System.IO;
using AssemblyApp.Data;
using UnityEngine;

/// <summary>
/// Загружает и парсит JSON с данными о сборке.
/// Предоставляет доступ к структуре дерева и связям с объектами сцены.
/// </summary>
public class DataLoader : MonoBehaviour
{
    [Header("Настройки")]
    [Tooltip("Имя JSON файла в папке Resources (без расширения)")]
    public string jsonFileName = "AssemblyData";

    private AssemblyDataRoot _data;
    
    // Кэш найденных объектов сцены для быстрого доступа
    private Dictionary<string, GameObject> _sceneObjectCache;
    // Кэш загруженных шагов
    private Dictionary<string, DisassemblyStep> _stepCache;

    public AssemblyDataRoot Data => _data;

    private void Awake()
    {
        LoadData();
        CacheSceneObjects();
        CacheSteps();
    }

    private void LoadData()
    {
        TextAsset jsonFile = Resources.Load<TextAsset>(jsonFileName);
        if (jsonFile == null)
        {
            Debug.LogError($"[DataLoader] Не найден файл {jsonFileName}.json в папке Resources!");
            return;
        }

        try
        {
            _data = JsonUtility.FromJson<AssemblyDataRoot>(jsonFile.text);
            Debug.Log($"[DataLoader] Данные загружены. Группа: {_data.rootName}, элементов групп: {_data.groups.Count}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[DataLoader] Ошибка парсинга JSON: {e.Message}");
        }
    }

    /// <summary>
    /// Проходит по всем объектам в сцене и создает словарь для быстрого поиска по имени
    /// </summary>
    private void CacheSceneObjects()
    {
        _sceneObjectCache = new Dictionary<string, GameObject>();
        GameObject[] allObjects = FindObjectsOfType<GameObject>();

        foreach (var obj in allObjects)
        {
            if (!_sceneObjectCache.ContainsKey(obj.name))
            {
                _sceneObjectCache.Add(obj.name, obj);
            }
        }
        Debug.Log($"[DataLoader] Закэшировано объектов сцены: {_sceneObjectCache.Count}");
    }

    /// <summary>
    /// Предзагружает все ScriptableObject шаги по именам
    /// </summary>
    private void CacheSteps()
    {
        _stepCache = new Dictionary<string, DisassemblyStep>();
        if (_data == null || _data.groups == null) return;

        // Рекурсивный обход для сбора всех имен шагов
        CollectStepNames(_data.groups);
        
        Debug.Log($"[DataLoader] Закэшировано шагов: {_stepCache.Count}");
    }

    private void CollectStepNames(List<AssemblyNode> nodes)
    {
        if (nodes == null) return;

        foreach (var node in nodes)
        {
            if (!string.IsNullOrEmpty(node.stepAssetName) && !_stepCache.ContainsKey(node.stepAssetName))
            {
                DisassemblyStep step = Resources.Load<DisassemblyStep>("Steps/" + node.stepAssetName);
                if (step != null)
                {
                    _stepCache.Add(node.stepAssetName, step);
                }
                else
                {
                    Debug.LogWarning($"[DataLoader] Шаг '{node.stepAssetName}' не найден в Resources/Steps/");
                }
            }

            if (node.IsGroup)
            {
                CollectStepNames(node.children);
            }
        }
    }

    /// <summary>
    /// Получить GameObject по данным узла
    /// </summary>
    public GameObject GetGameObjectByNode(AssemblyNode node)
    {
        if (string.IsNullOrEmpty(node.objectName)) return null;
        
        if (_sceneObjectCache.TryGetValue(node.objectName, out GameObject obj))
        {
            return obj;
        }
        
        Debug.LogWarning($"[DataLoader] Объект '{node.objectName}' не найден в сцене!");
        return null;
    }

    /// <summary>
    /// Получить шаг разборки по данным узла
    /// </summary>
    public DisassemblyStep GetStepByNode(AssemblyNode node)
    {
        if (string.IsNullOrEmpty(node.stepAssetName)) return null;

        if (_stepCache.TryGetValue(node.stepAssetName, out DisassemblyStep step))
        {
            return step;
        }
        return null;
    }
}
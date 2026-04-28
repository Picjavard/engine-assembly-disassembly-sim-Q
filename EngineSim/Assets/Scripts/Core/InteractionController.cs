using UnityEngine;
using UnityEngine.InputSystem;
using DG.Tweening;
using AssemblyApp.Data;
using System.Collections.Generic;

/// <summary>
/// Контроллер взаимодействия с 3D-объектами.
/// Обрабатывает клики ЛКМ (выделение) и ПКМ (разборка/сборка).
/// Интегрирован с SequenceManager, HierarchyView и PartInfoPanel.
/// </summary>
[RequireComponent(typeof(Camera))]
public class InteractionController : MonoBehaviour
{
    [Header("Ссылки на менеджеры")]
    [Tooltip("Менеджер последовательности разборки/сборки")]
    public SequenceManager sequenceManager;

    [Tooltip("Загрузчик данных JSON")]
    public DataLoader dataLoader;

    [Tooltip("Контроллер камеры")]
    public OrbitCameraController cameraController;

    [Tooltip("Панель информации о детали")]
    public PartInfoPanel infoPanel;

    [Tooltip("Представление иерархии (дерево)")]
    public HierarchyView hierarchyView;

    [Header("Настройки взаимодействия")]
    [Tooltip("Слой деталей для raycast")]
    public LayerMask partsLayer;

    [Tooltip("Длительность подсветки при клике (сек)")]
    public float highlightDuration = 0.5f;

    [Tooltip("Цвет подсветки при выделении")]
    public Color highlightColor = new Color(1f, 0.93f, 0f, 1f); // Ярко-желтый (RGB: 255, 238, 0)

    [Header("Таймер (опционально)")]
    [Tooltip("Таймер для задержек между действиями")]
    public CountdownTimer actionTimer;

    // Текущий выбранный объект
    private GameObject _currentSelectedObject;
    private AssemblyNode _currentSelectedNode;

    // MaterialPropertyBlock для подсветки
    private int _emissionId;
    private MaterialPropertyBlock _block;

    private Camera _cam;

    // Ссылки на действия New Input System
    private Mouse _mouse;

    private void Awake()
    {
        _cam = GetComponent<Camera>();
        _emissionId = Shader.PropertyToID("_EmissionColor");
        _block = new MaterialPropertyBlock();

        // Инициализация New Input System
        _mouse = Mouse.current;

        if (_cam == null)
        {
            Debug.LogError("[InteractionController] Камера не найдена!");
            enabled = false;
        }
    }

    private void Update()
    {
        HandleMouseInput();
    }

    /// <summary>
    /// Обработка ввода мыши с использованием New Input System
    /// </summary>
    private void HandleMouseInput()
    {
        if (_cam == null || sequenceManager == null || sequenceManager.isAnimating)
        {
            return; // Блокируем ввод во время анимации
        }

        // Проверка на случай, если устройство мыши не подключено
        if (_mouse == null)
            return;

        // Получаем позицию мыши через New Input System
        Vector2 mousePosition = _mouse.position.ReadValue();
        Ray ray = _cam.ScreenPointToRay(mousePosition);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, Mathf.Infinity, partsLayer))
        {
            GameObject hitObject = hit.collider.gameObject;

            // ЛКМ - Выделение
            if (_mouse.leftButton.wasPressedThisFrame)
            {
                OnLeftClick(hitObject);
            }

            // ПКМ - Разборка/Сборка
            if (_mouse.rightButton.wasPressedThisFrame)
            {
                OnRightClick(hitObject);
            }
        }
        else
        {
            // Клик в пустоту - сброс выделения (опционально)
            if (_mouse.leftButton.wasPressedThisFrame)
            {
                // ClearSelection();
            }
        }
    }

    /// <summary>
    /// Обработка клика ЛКМ по детали
    /// </summary>
private void OnLeftClick(GameObject hitObject)
    {
        if (hitObject == null) return;

        // Сброс старого выделения
        if (_currentSelectedObject != null && _currentSelectedObject != hitObject)
        {
            DisableOutline(_currentSelectedObject);
        }

        _currentSelectedObject = hitObject;
        _currentSelectedNode = FindNodeByObjectName(hitObject.name);

        // Включаем контур
        EnableOutline(hitObject, highlightColor, 0.008f);

        // 3. Обновляем инфопанель и иерархию (без изменений)
        if (infoPanel != null)
        {
            string title = _currentSelectedNode != null ? _currentSelectedNode.name : hitObject.name;
            string description = _currentSelectedNode != null ? _currentSelectedNode.description : "Описание отсутствует.";
            infoPanel.UpdateInfo(title, description);
        }

        if (hierarchyView != null && _currentSelectedNode != null)
        {
            hierarchyView.SelectPartByNode(_currentSelectedNode);
        }

        Debug.Log($"[InteractionController] Выбрана деталь: {hitObject.name}");
    }
    /// <summary>
    /// Обработка клика ПКМ по детали (разборка/сборка)
    /// </summary>
    private void OnRightClick(GameObject hitObject)
    {
        if (hitObject == null) return;

        PartController partController = hitObject.GetComponent<PartController>();

        if (partController == null || partController.associatedStep == null)
        {
            Debug.LogWarning($"[InteractionController] У объекта {hitObject.name} нет шага разборки!");
            return;
        }

        bool isDisassembled = partController.isDisassembled;

        if (!isDisassembled)
        {
            TryExecuteDisassembly(partController);
        }
        else
        {
            TryExecuteAssembly(partController);
        }
    }

    /// <summary>
    /// Попытка выполнить разборку детали
    /// </summary>
    private void TryExecuteDisassembly(PartController part)
    {
        if (sequenceManager == null) return;

        PartController nextPart = GetNextDisassemblyPart();

        if (nextPart == part)
        {
            Debug.Log($"[InteractionController] Запуск разборки: {part.name}");
            sequenceManager.NextStep();
        }
        else
        {
            Debug.LogWarning($"[InteractionController] Нельзя разобрать {part.name}! Сначала разберите предыдущие детали.");
            FlashError(part);
        }
    }

    /// <summary>
    /// Попытка выполнить сборку детали
    /// </summary>
    private void TryExecuteAssembly(PartController part)
    {
        if (sequenceManager == null) return;

        PartController lastPart = GetLastDisassembledPart();

        if (lastPart == part)
        {
            Debug.Log($"[InteractionController] Запуск сборки: {part.name}");
            sequenceManager.PreviousStep();
        }
        else
        {
            Debug.LogWarning($"[InteractionController] Нельзя собрать {part.name}! Сначала соберите последующие детали.");
            FlashError(part);
        }
    }

    /// <summary>
    /// Получить следующую деталь для разборки
    /// </summary>
    private PartController GetNextDisassemblyPart()
    {
        if (sequenceManager == null) return null;

        foreach (var part in sequenceManager.allParts)
        {
            if (!part.isDisassembled && part.associatedStep != null)
            {
                return part;
            }
        }
        return null;
    }

    /// <summary>
    /// Получить последнюю разобранную деталь (для сборки)
    /// </summary>
    private PartController GetLastDisassembledPart()
    {
        if (sequenceManager == null) return null;

        for (int i = sequenceManager.allParts.Count - 1; i >= 0; i--)
        {
            if (sequenceManager.allParts[i].isDisassembled &&
                sequenceManager.allParts[i].associatedStep != null)
            {
                return sequenceManager.allParts[i];
            }
        }
        return null;
    }

    /// <summary>
    /// Найти узел данных по имени объекта
    /// </summary>
    private AssemblyNode FindNodeByObjectName(string objectName)
    {
        if (dataLoader == null || dataLoader.Data == null) return null;
        return FindNodeRecursive(dataLoader.Data.groups, objectName);
    }

    /// <summary>
    /// Рекурсивный поиск узла по имени объекта
    /// </summary>
    private AssemblyNode FindNodeRecursive(List<AssemblyNode> nodes, string objectName)
    {
        if (nodes == null) return null;

        foreach (var node in nodes)
        {
            if (node.objectName == objectName)
            {
                return node;
            }

            if (node.IsGroup && node.children != null)
            {
                var found = FindNodeRecursive(node.children, objectName);
                if (found != null) return found;
            }
        }
        return null;
    }

    /// <summary>
    /// Подсветка объекта через MaterialPropertyBlock.
    /// Работает независимо от прозрачности, так как меняет только _EmissionColor.
    /// </summary>
    private void HighlightObject(GameObject target, float duration)
    {
        if (target == null) return;
        Renderer renderer = target.GetComponent<Renderer>();
        if (renderer == null) return;

        // Если вдруг на объекте старый шейдер, предупреждаем (опционально)
        if (!renderer.sharedMaterial.HasProperty("_EmissionColor"))
        {
            Debug.LogWarning($"[Highlight] Шейдер {renderer.sharedMaterial.name} не поддерживает эмиссию!");
            return;
        }

        Color targetEmission = highlightColor * 0.8f; 
        float halfDuration = duration / 2f;

        // Используем DOTween для анимации значения от 0 до 1 и обратно
        DOTween.To(() => 0f, x => {
                if (renderer == null) return;

                renderer.GetPropertyBlock(_block);

                // Плавное нарастание и затухание (Fade In / Fade Out)
                float alpha = x < halfDuration 
                    ? Mathf.SmoothStep(0f, 1f, x / halfDuration)
                    : Mathf.SmoothStep(1f, 0f, (x - halfDuration) / halfDuration);

                // ВАЖНО: Меняем ТОЛЬКО эмиссию. 
                // Прозрачность (_Color) остается нетронутой и управляется отдельно через UIManager.
                _block.SetColor("_EmissionColor", targetEmission * alpha);
                renderer.SetPropertyBlock(_block);
                
            }, 0f, duration)
            .SetEase(Ease.Linear)
            .OnComplete(() => {
                // Гарантированная очистка после завершения
                if (renderer != null)
                {
                    renderer.GetPropertyBlock(_block);
                    _block.SetColor("_EmissionColor", Color.black);
                    renderer.SetPropertyBlock(_block);
                }
            });
    }

    /// <summary>
    /// Красная вспышка ошибки. Аналогично подсветке.
    /// </summary>
    private void FlashError(PartController part)
    {
        if (part == null) return;
        Renderer renderer = part.GetComponent<Renderer>();
        if (renderer == null) return;

        Color errorColor = new Color(1f, 0f, 0f, 1f) * 0.8f;
        float flashDuration = 0.5f;
        float halfDuration = flashDuration / 2f;

        DOTween.To(() => 0f, x => {
                if (renderer == null) return;
                renderer.GetPropertyBlock(_block);

                float alpha = x < halfDuration 
                    ? Mathf.SmoothStep(0f, 1f, x / halfDuration)
                    : Mathf.SmoothStep(1f, 0f, (x - halfDuration) / halfDuration);

                _block.SetColor("_EmissionColor", errorColor * alpha);
                renderer.SetPropertyBlock(_block);
                
            }, 0f, flashDuration)
            .SetEase(Ease.Linear)
            .OnComplete(() => {
                if (renderer != null)
                {
                    renderer.GetPropertyBlock(_block);
                    _block.SetColor("_EmissionColor", Color.black);
                    renderer.SetPropertyBlock(_block);
                }
            });
    }

 // === OUTLINE CONTROLS ===

     /// <summary>
    /// Включить контур (установить ширину > 0)
    /// </summary>
    public void EnableOutline(GameObject target, Color outlineColor, float width = 0.005f)
    {
        if (target == null) return;
        Renderer renderer = target.GetComponent<Renderer>();
        if (renderer == null) return;

        renderer.GetPropertyBlock(_block);
        _block.SetColor("_OutlineColor", outlineColor);
        _block.SetFloat("_OutlineWidth", Mathf.Max(0.0001f, width)); // Гарантируем > 0
        renderer.SetPropertyBlock(_block);
    }

    /// <summary>
    /// Выключить контур (установить ширину = 0)
    /// </summary>
    public void DisableOutline(GameObject target)
    {
        if (target == null) return;
        Renderer renderer = target.GetComponent<Renderer>();
        if (renderer == null) return;

        renderer.GetPropertyBlock(_block);
        _block.SetFloat("_OutlineWidth", 0f); // 0 = контур не рисуется
        renderer.SetPropertyBlock(_block);
    }

    /// <summary>
    /// Сброс текущего выделения
    /// </summary>
    public void ClearSelection()
    {
        if (_currentSelectedObject != null)
        {
            DisableOutline(_currentSelectedObject);
        }
        
        _currentSelectedObject = null;
        _currentSelectedNode = null;

        if (infoPanel != null)
        {
            infoPanel.ClearInfo();
        }

        Debug.Log("[InteractionController] Выделение сброшено.");
    }

    /// <summary>
    /// Получить текущий выбранный объект
    /// </summary>
    public GameObject GetCurrentSelectedObject()
    {
        return _currentSelectedObject;
    }
}
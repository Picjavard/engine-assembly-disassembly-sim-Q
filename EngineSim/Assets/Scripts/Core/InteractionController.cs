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

    [Tooltip("Цвет подсветки при выделении (будет использован как базовая эмиссия)")]
    public Color highlightColor = new Color(1f, 0.93f, 0f, 1f); // Ярко-желтый (RGB: 255, 238, 0)

    [Header("Эмиссия (подсветка)")]
    [Tooltip("Множитель яркости эмиссии для выделения")]
    public float selectionEmissionMultiplier = 0.9f;

    [Tooltip("Длительность плавного появления подсветки выделения")]
    public float selectionEmissionFadeDuration = 0.15f;

    [Tooltip("Длительность вспышки ошибки")]
    public float errorFlashDuration = 0.5f;

    [Tooltip("Множитель яркости эмиссии для ошибки")]
    public float errorEmissionMultiplier = 1.0f;

    [Header("Таймер (опционально)")]
    [Tooltip("Таймер для задержек между действиями")]
    public CountdownTimer actionTimer;

    // Текущий выбранный объект
    private GameObject _currentSelectedObject;
    private AssemblyNode _currentSelectedNode;

    // MaterialPropertyBlock для подсветки
    private int _emissionId;
    private MaterialPropertyBlock _block;

    // Сохраняем базовую эмиссию для корректного восстановления при смене выбора
    private readonly Dictionary<Renderer, Color> _baseEmissions = new Dictionary<Renderer, Color>();

    // Твины для анимаций эмиссии
    private Tween _selectionTween;
    private Tween _errorTween;

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
        RestoreBaseEmission();
        }

        _currentSelectedObject = hitObject;
        _currentSelectedNode = FindNodeByObjectName(hitObject.name);

    // Включаем эмиссию выделения (без контура)
    SelectWithEmission(hitObject);

    // Обновляем инфопанель через HierarchyView, чтобы не было дублей.
    // Если узел не найден — покажем заглушку.
    if (hierarchyView != null && _currentSelectedNode != null)
    {
        hierarchyView.SelectPartByNode(_currentSelectedNode);
    }
    else if (infoPanel != null)
    {
        infoPanel.UpdateInfo(hitObject.name, "Описание отсутствует.");
    }

        Debug.Log($"[InteractionController] Выбрана деталь: {hitObject.name}");
    }
    /// <summary>
    /// Обработка клика ПКМ по детали (разборка/сборка)
    /// </summary>
    private void OnRightClick(GameObject hitObject)
    {
        if (hitObject == null) return;

    // Подсветка по ПКМ, чтобы было визуально понятно, по какой детали выполняется действие
    if (_currentSelectedObject == null || _currentSelectedObject != hitObject)
    {
        RestoreBaseEmission();
        _currentSelectedObject = hitObject;
        _currentSelectedNode = FindNodeByObjectName(hitObject.name);
        SelectWithEmission(hitObject);
    }

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

        Renderer[] renderers = part.GetComponentsInChildren<Renderer>();
        if (renderers == null || renderers.Length == 0) return;

        // Останавливаем возможную анимацию выделения, чтобы эмиссия не "дёргалась" в конкурирующих tween.
        _selectionTween?.Kill();
        _selectionTween = null;

        _errorTween?.Kill();

        // Восстановим эмиссию ровно в то состояние, которое было на момент вспышки.
        var restoreEmissions = new Dictionary<Renderer, Color>(renderers.Length);
        foreach (var r in renderers)
        {
            if (r == null) continue;
            EnsureEmissionEnabled(r);
            restoreEmissions[r] = GetCurrentEmission(r);
        }

        Color errorColor = new Color(1f, 0f, 0f, 1f) * errorEmissionMultiplier;
        float duration = Mathf.Max(0.01f, errorFlashDuration);

        _errorTween = DOVirtual.Float(0f, 1f, duration, t =>
        {
            // Две фазы: нарастание (0..0.5) и затухание (0.5..1)
            float k;
            if (t < 0.5f) k = Mathf.SmoothStep(0f, 1f, t / 0.5f);
            else k = Mathf.SmoothStep(1f, 0f, (t - 0.5f) / 0.5f);

            foreach (var kv in restoreEmissions)
            {
                var r = kv.Key;
                if (r == null) continue;
                Color from = kv.Value;
                Color emission = Color.Lerp(from, errorColor, k);
                SetEmission(r, emission);
            }
        })
        .SetEase(Ease.Linear)
        .OnComplete(() =>
        {
            foreach (var kv in restoreEmissions)
            {
                if (kv.Key == null) continue;
                // Если это текущая выделенная деталь — возвращаем к "полной" яркости выделения.
                if (_baseEmissions.ContainsKey(kv.Key))
                    SetEmission(kv.Key, highlightColor * selectionEmissionMultiplier);
                else
                    SetEmission(kv.Key, kv.Value);
            }
        });
    }

    private void SelectWithEmission(GameObject target)
    {
        if (target == null) return;

        _selectionTween?.Kill();
        _selectionTween = null;
        _errorTween?.Kill();
        _errorTween = null;

        Renderer[] renderers = target.GetComponentsInChildren<Renderer>();
        if (renderers == null || renderers.Length == 0) return;

        _baseEmissions.Clear();

        // Сохраняем базовую эмиссию каждого рендера, чтобы потом корректно восстановить
        foreach (var r in renderers)
        {
            if (r == null) continue;
            EnsureEmissionEnabled(r);
            _baseEmissions[r] = GetBaseEmission(r);
        }

        Color targetEmission = highlightColor * selectionEmissionMultiplier;

        float duration = Mathf.Max(0.01f, selectionEmissionFadeDuration);
        _selectionTween = DOVirtual.Float(0f, 1f, duration, t =>
        {
            foreach (var kv in _baseEmissions)
            {
                var r = kv.Key;
                if (r == null) continue;
                Color baseEmission = kv.Value;
                SetEmission(r, Color.Lerp(baseEmission, targetEmission, t));
            }
        }).SetEase(Ease.OutCubic);
    }

    private void RestoreBaseEmission()
    {
        _selectionTween?.Kill();
        _selectionTween = null;
        _errorTween?.Kill();
        _errorTween = null;

        if (_baseEmissions.Count == 0) return;

        foreach (var kv in _baseEmissions)
        {
            var r = kv.Key;
            if (r == null) continue;
            EnsureEmissionEnabled(r);
            SetEmission(r, kv.Value);
        }

        _baseEmissions.Clear();
    }

    private Color GetBaseEmission(Renderer renderer)
    {
        if (renderer == null) return Color.black;
        if (renderer.sharedMaterial != null && renderer.sharedMaterial.HasProperty("_EmissionColor"))
        {
            return renderer.sharedMaterial.GetColor("_EmissionColor");
        }
        return Color.black;
    }

    private Color GetCurrentEmission(Renderer renderer)
    {
        if (renderer == null) return Color.black;

        renderer.GetPropertyBlock(_block);
        if (_block != null && _block.HasProperty(_emissionId))
        {
            return _block.GetColor(_emissionId);
        }

        return GetBaseEmission(renderer);
    }

    private void EnsureEmissionEnabled(Renderer renderer)
    {
        if (renderer == null || renderer.sharedMaterial == null) return;
        if (!renderer.sharedMaterial.HasProperty("_EmissionColor")) return;

        // В shader эмиссия включена вариативностью через keyword — включаем, чтобы установка _EmissionColor визуально работала.
        renderer.sharedMaterial.EnableKeyword("_EMISSION");
    }

    private void SetEmission(Renderer renderer, Color emission)
    {
        if (renderer == null) return;
        renderer.GetPropertyBlock(_block);
        _block.SetColor(_emissionId, emission);
        renderer.SetPropertyBlock(_block);
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
        RestoreBaseEmission();
        _selectionTween?.Kill();
        _selectionTween = null;
        _errorTween?.Kill();
        _errorTween = null;

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
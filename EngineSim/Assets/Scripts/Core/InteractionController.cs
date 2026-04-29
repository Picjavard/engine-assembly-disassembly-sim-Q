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

    [Tooltip("Сколько времени держать подсветку выделения на максимальной яркости (сек)")]
    public float selectionEmissionHoldDuration = 1.0f;

    [Tooltip("Длительность плавного затухания подсветки выделения (сек)")]
    public float selectionEmissionFadeOutDuration = 0.5f;

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

        PartController partController = hitObject.GetComponent<PartController>();

        if (partController == null || partController.associatedStep == null)
        {
            Debug.LogWarning($"[InteractionController] У объекта {hitObject.name} нет шага разборки!");
            return;
        }

        // Подсветка по ПКМ, чтобы было визуально понятно, по какой детали выполняется действие.
        // Перезапускаем glow даже если кликнули по той же самой детали повторно.
        if (_currentSelectedObject == null || _currentSelectedObject != hitObject)
        {
            RestoreBaseEmission();
            _currentSelectedObject = hitObject;
            _currentSelectedNode = FindNodeByObjectName(hitObject.name);
        }
        SelectWithEmission(hitObject);

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
    /// Красная вспышка ошибки. Аналогично подсветке.
    /// </summary>
    private void FlashError(PartController part)
    {
        if (part == null) return;

        Renderer[] renderers = part.GetComponentsInChildren<Renderer>();
        if (renderers == null || renderers.Length == 0) return;

        // Сохраняем текущее состояние эмиссии, чтобы корректно откатить после ошибки.
        var originalEmissions = new Dictionary<Renderer, Color>(renderers.Length);
        foreach (var r in renderers)
        {
            if (r == null) continue;
            EnsureEmissionEnabled(r);
            originalEmissions[r] = GetCurrentEmission(r);
        }

        // Если ошибка на текущей выделенной детали — после вспышки перезапускаем подсветку.
        bool shouldRestartSelection = _currentSelectedObject != null && part.gameObject == _currentSelectedObject;

        // Останавливаем подсветку выделения, чтобы ошибка не конфликтовала.
        _selectionTween?.Kill();
        _selectionTween = null;

        _errorTween?.Kill();

        Color errorColor = new Color(1f, 0f, 0f, 1f) * errorEmissionMultiplier;
        float duration = Mathf.Max(0.01f, errorFlashDuration);

        _errorTween = DOVirtual.Float(0f, 1f, duration, t =>
        {
            // Две фазы: нарастание (0..0.5) и затухание (0.5..1)
            float k;
            if (t < 0.5f) k = Mathf.SmoothStep(0f, 1f, t / 0.5f);
            else k = Mathf.SmoothStep(1f, 0f, (t - 0.5f) / 0.5f);

            foreach (var kv in originalEmissions)
            {
                var r = kv.Key;
                if (r == null) continue;
                SetEmission(r, Color.Lerp(kv.Value, errorColor, k));
            }
        })
        .SetEase(Ease.Linear)
        .OnComplete(() =>
        {
            // Откат к исходной эмиссии
            foreach (var kv in originalEmissions)
            {
                if (kv.Key == null) continue;
                SetEmission(kv.Key, kv.Value);
            }

            // Перезапускаем подсветку выделения, если ошибка была на ней
            if (shouldRestartSelection)
            {
                SelectWithEmission(_currentSelectedObject);
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

        float fadeIn = Mathf.Max(0.01f, selectionEmissionFadeDuration);
        float hold = Mathf.Max(0f, selectionEmissionHoldDuration);
        float fadeOut = Mathf.Max(0.01f, selectionEmissionFadeOutDuration);

        float total = fadeIn + hold + fadeOut;
        if (total <= 0.001f) return;

        _selectionTween = DOVirtual.Float(0f, total, total, elapsed =>
        {
            float k;
            if (elapsed <= fadeIn)
            {
                k = Mathf.SmoothStep(0f, 1f, elapsed / fadeIn);
            }
            else if (elapsed <= fadeIn + hold)
            {
                k = 1f;
            }
            else
            {
                float x = (elapsed - fadeIn - hold) / fadeOut; // 0..1
                k = Mathf.SmoothStep(1f, 0f, x);
            }

            foreach (var kv in _baseEmissions)
            {
                var r = kv.Key;
                if (r == null) continue;
                Color baseEmission = kv.Value;
                SetEmission(r, Color.Lerp(baseEmission, targetEmission, k));
            }
        })
        .SetEase(Ease.Linear)
        .OnComplete(() =>
        {
            // Финальный откат к базовой эмиссии
            foreach (var kv in _baseEmissions)
            {
                var r = kv.Key;
                if (r == null) continue;
                EnsureEmissionEnabled(r);
                SetEmission(r, kv.Value);
            }
            _baseEmissions.Clear();
            _selectionTween = null;
        });
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
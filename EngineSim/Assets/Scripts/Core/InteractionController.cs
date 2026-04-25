using UnityEngine;
using DG.Tweening;
using AssemblyApp.Data;

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
    public Color highlightColor = new Color(1f, 0.8f, 0f, 1f);

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

    private void Awake()
    {
        _cam = GetComponent<Camera>();
        _emissionId = Shader.PropertyToID("_EmissionColor");
        _block = new MaterialPropertyBlock();

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
    /// Обработка ввода мыши
    /// </summary>
    private void HandleMouseInput()
    {
        if (_cam == null || sequenceManager == null || sequenceManager.isAnimating)
        {
            return; // Блокируем ввод во время анимации
        }

        Ray ray = _cam.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, Mathf.Infinity, partsLayer))
        {
            GameObject hitObject = hit.collider.gameObject;

            // ЛКМ - Выделение
            if (Input.GetMouseButtonDown(0))
            {
                OnLeftClick(hitObject);
            }

            // ПКМ - Разборка/Сборка
            if (Input.GetMouseButtonDown(1))
            {
                OnRightClick(hitObject);
            }
        }
        else
        {
            // Клик в пустоту - сброс выделения (опционально)
            if (Input.GetMouseButtonDown(0))
            {
                // ClearSelection(); // Можно раскомментировать, если нужно сбрасывать выделение
            }
        }
    }

    /// <summary>
    /// Обработка клика ЛКМ по детали
    /// </summary>
    private void OnLeftClick(GameObject hitObject)
    {
        if (hitObject == null) return;

        _currentSelectedObject = hitObject;

        // Находим данные узла по объекту
        _currentSelectedNode = FindNodeByObjectName(hitObject.name);

        // Подсветка с плавным нарастанием и затуханием
        HighlightObject(hitObject, highlightDuration);

        // Обновление информационной панели
        if (infoPanel != null)
        {
            string title = _currentSelectedNode != null ? _currentSelectedNode.name : hitObject.name;
            string description = _currentSelectedNode != null ? _currentSelectedNode.description : "Описание отсутствует.";
            infoPanel.UpdateInfo(title, description);
        }

        // Синхронизация с деревом иерархии
        if (hierarchyView != null && _currentSelectedNode != null)
        {
            hierarchyView.SelectPartByNode(_currentSelectedNode);
        }

        // Фокусировка камеры (опционально, можно добавить двойной клик)
        // if (cameraController != null)
        // {
        //     cameraController.FocusOnObject(hitObject, 1f, 3f);
        // }

        Debug.Log($"[InteractionController] Выбрана деталь: {hitObject.name}");
    }

    /// <summary>
    /// Обработка клика ПКМ по детали (разборка/сборка)
    /// </summary>
    private void OnRightClick(GameObject hitObject)
    {
        if (hitObject == null) return;

        // Проверяем, есть ли у этой детали PartController
        PartController partController = hitObject.GetComponent<PartController>();

        if (partController == null || partController.associatedStep == null)
        {
            Debug.LogWarning($"[InteractionController] У объекта {hitObject.name} нет шага разборки!");
            return;
        }

        // Определяем текущее состояние детали
        bool isDisassembled = partController.isDisassembled;

        // Проверка порядка разборки: нельзя разобрать деталь, если предыдущие еще не разобраны
        // (эта логика уже реализована в SequenceManager, но можно добавить дополнительную проверку здесь)

        if (!isDisassembled)
        {
            // Деталь установлена -> Запуск разборки
            TryExecuteDisassembly(partController);
        }
        else
        {
            // Деталь снята -> Запуск сборки
            TryExecuteAssembly(partController);
        }
    }

    /// <summary>
    /// Попытка выполнить разборку детали
    /// </summary>
    private void TryExecuteDisassembly(PartController part)
    {
        if (sequenceManager == null) return;

        // Проверяем, является ли эта деталь следующим шагом в последовательности
        PartController nextPart = GetNextDisassemblyPart();

        if (nextPart == part)
        {
            // Это следующий шаг -> выполняем
            Debug.Log($"[InteractionController] Запуск разборки: {part.name}");
            sequenceManager.NextStep();
        }
        else
        {
            // Не следующий шаг -> блокируем
            Debug.LogWarning($"[InteractionController] Нельзя разобрать {part.name}! Сначала разберите предыдущие детали.");

            // Визуальная обратная связь (например, красная вспышка)
            FlashError(part);
        }
    }

    /// <summary>
    /// Попытка выполнить сборку детали
    /// </summary>
    private void TryExecuteAssembly(PartController part)
    {
        if (sequenceManager == null) return;

        // Проверяем, является ли эта деталь последним разобранным шагом
        PartController lastPart = GetLastDisassembledPart();

        if (lastPart == part)
        {
            // Это последний шаг -> выполняем откат
            Debug.Log($"[InteractionController] Запуск сборки: {part.name}");
            sequenceManager.PreviousStep();
        }
        else
        {
            // Не последний шаг -> блокируем (сборка должна идти в обратном порядке)
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
    private AssemblyNode FindNodeRecursive(System.Collections.Generic.List<AssemblyNode> nodes, string objectName)
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
    /// Подсветка объекта на заданное время с плавным нарастанием и затуханием.
    /// Общая длительность: 0.5 сек (0.25 сек нарастание + 0.25 сек затухание).
    /// </summary>
    private void HighlightObject(GameObject target, float duration)
    {
        if (target == null) return;

        Renderer renderer = target.GetComponent<Renderer>();
        if (renderer == null) return;

        Color targetColor = highlightColor * 0.8f;
        float halfDuration = duration / 2f;

        // Используем DOTween для плавной анимации: нарастание + затухание
        DG.Tweening.DOTween.To(() => 0f, x => {
            if (renderer == null) return;

            renderer.GetPropertyBlock(_block);

            // Рассчитываем коэффициент с плавным нарастанием и затуханием
            float alpha;
            if (x < halfDuration)
            {
                // Первая половина: плавное нарастание (0 -> 1)
                alpha = Mathf.SmoothStep(0f, 1f, (x / halfDuration));
            }
            else
            {
                // Вторая половина: плавное затухание (1 -> 0)
                alpha = Mathf.SmoothStep(1f, 0f, ((x - halfDuration) / halfDuration));
            }

            Color currentColor = targetColor * alpha;
            _block.SetColor(_emissionId, currentColor);
            renderer.SetPropertyBlock(_block);
        }, 0f, duration)
        .SetEase(DG.Tweening.Ease.Linear)
        .OnComplete(() => {
            // Гарантированно выключаем подсветку после завершения
            if (renderer != null)
            {
                _block.Clear();
                renderer.SetPropertyBlock(_block);
            }
        });
    }

    /// <summary>
    /// Красная вспышка при ошибке (плавное нарастание и затухание за 0.5 сек)
    /// </summary>
    private void FlashError(PartController part)
    {
        if (part == null) return;

        Renderer renderer = part.GetComponent<Renderer>();
        if (renderer == null) return;

        Color errorColor = new Color(1f, 0f, 0f, 1f) * 0.8f;
        float flashDuration = 0.5f;
        float halfDuration = flashDuration / 2f;

        // Используем DOTween для плавной анимации: нарастание + затухание
        DG.Tweening.DOTween.To(() => 0f, x => {
            if (renderer == null) return;

            renderer.GetPropertyBlock(_block);

            // Рассчитываем коэффициент с плавным нарастанием и затуханием
            float alpha;
            if (x < halfDuration)
            {
                // Первая половина: плавное нарастание (0 -> 1)
                alpha = Mathf.SmoothStep(0f, 1f, (x / halfDuration));
            }
            else
            {
                // Вторая половина: плавное затухание (1 -> 0)
                alpha = Mathf.SmoothStep(1f, 0f, ((x - halfDuration) / halfDuration));
            }

            Color currentColor = errorColor * alpha;
            _block.SetColor(_emissionId, currentColor);
            renderer.SetPropertyBlock(_block);
        }, 0f, flashDuration)
        .SetEase(DG.Tweening.Ease.Linear)
        .OnComplete(() => {
            // Полностью убираем подсветку после завершения анимации
            if (renderer != null)
            {
                _block.Clear();
                renderer.SetPropertyBlock(_block);
            }
        });
    }

    /// <summary>
    /// Сброс текущего выделения
    /// </summary>
    public void ClearSelection()
    {
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
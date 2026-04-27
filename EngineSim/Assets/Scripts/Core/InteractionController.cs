using UnityEngine;
using UnityEngine.InputSystem;
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
    private int _colorId;
    private MaterialPropertyBlock _block;

    // Для хранения исходного цвета (включая альфа-канал прозрачности)
    private Color _originalColor;

    private Camera _cam;

    // Ссылки на действия New Input System
    private Mouse _mouse;

    private void Awake()
    {
        _cam = GetComponent<Camera>();
        _emissionId = Shader.PropertyToID("_EmissionColor");
        _colorId = Shader.PropertyToID("_Color");
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

            // ЛКМ - Выделение (GetMouseButtonDown(0) -> leftButton.wasPressedThisFrame)
            if (_mouse.leftButton.wasPressedThisFrame)
            {
                OnLeftClick(hitObject);
            }

            // ПКМ - Разборка/Сборка (GetMouseButtonDown(1) -> rightButton.wasPressedThisFrame)
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
    /// ВАЖНО: Сохраняет цвет и прозрачность объекта, меняя только эмиссию.
    /// </summary>
    private void HighlightObject(GameObject target, float duration)
    {
        if (target == null) return;

        Renderer renderer = target.GetComponent<Renderer>();
        if (renderer == null)
        {
            Debug.LogWarning($"[HighlightObject] У объекта {target.name} нет Renderer!");
            return;
        }

        // Проверяем, поддерживает ли материал свойство _EmissionColor
        renderer.GetPropertyBlock(_block);

        // Сохраняем текущий цвет объекта (включая альфа-канал) перед изменением
        _originalColor = _block.GetColor(_colorId);

        // Проверка: если материал не поддерживает эмиссию, пробуем установить её явно
        // Это нужно для материалов, где эмиссия не была настроена в редакторе
        if (!_block.HasProperty(_emissionId))
        {
            // Принудительно устанавливаем черную эмиссию (база)
            _block.SetColor(_emissionId, Color.black);
        }

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

            // Восстанавливаем исходный цвет объекта (сохраняем прозрачность!)
            _block.SetColor(_colorId, _originalColor);

            renderer.SetPropertyBlock(_block);
        }, 0f, duration)
        .SetEase(DG.Tweening.Ease.Linear)
        .OnComplete(() => {
            // Гарантированно выключаем подсветку после завершения, но сохраняем цвет и прозрачность
            if (renderer != null)
            {
                renderer.GetPropertyBlock(_block);
                _block.SetColor(_emissionId, Color.black);
                _block.SetColor(_colorId, _originalColor);
                renderer.SetPropertyBlock(_block);
            }
        });

        Debug.Log($"[HighlightObject] Запущена подсветка объекта {target.name} длительностью {duration}с");
    }

    /// <summary>
    /// Красная вспышка при ошибке (плавное нарастание и затухание за 0.5 сек)
    /// ВАЖНО: Сохраняет цвет и прозрачность объекта, меняя только эмиссию.
    /// </summary>
    private void FlashError(PartController part)
    {
        if (part == null) return;

        Renderer renderer = part.GetComponent<Renderer>();
        if (renderer == null)
        {
            Debug.LogWarning($"[FlashError] У объекта {part.name} нет Renderer!");
            return;
        }

        // Проверяем, поддерживает ли материал свойство _EmissionColor
        renderer.GetPropertyBlock(_block);

        // Сохраняем текущий цвет объекта (включая альфа-канал)
        Color originalColor = _block.GetColor(_colorId);

        // Проверка: если материал не поддерживает эмиссию, пробуем установить её явно
        if (!_block.HasProperty(_emissionId))
        {
            _block.SetColor(_emissionId, Color.black);
        }

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

            // Восстанавливаем исходный цвет объекта (сохраняем прозрачность!)
            _block.SetColor(_colorId, originalColor);

            renderer.SetPropertyBlock(_block);
        }, 0f, flashDuration)
        .SetEase(DG.Tweening.Ease.Linear)
        .OnComplete(() => {
            // Полностью убираем подсветку после завершения анимации, но сохраняем цвет и прозрачность
            if (renderer != null)
            {
                renderer.GetPropertyBlock(_block);
                _block.SetColor(_emissionId, Color.black);
                _block.SetColor(_colorId, originalColor);
                renderer.SetPropertyBlock(_block);
            }
        });

        Debug.Log($"[FlashError] Запущена красная вспышка на объекте {part.name}");
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
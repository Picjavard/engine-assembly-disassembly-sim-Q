using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;

/// <summary>
/// Управляет игровым интерфейсом через UI Toolkit.
/// Требует компонент UIDocument на том же объекте или в сцене.
/// </summary>
public class UIManager : MonoBehaviour
{
    [Header("Ссылки")]
    [Tooltip("Ссылка на менеджер последовательности")]
    public SequenceManager sequenceManager;

    [Tooltip("Ссылка на хайлайтер (для прозрачности)")]
    public RaycastHighlighter highlighter;

    [Tooltip("Ссылка на панель информации о детали")]
    public PartInfoPanel infoPanel;

    [Tooltip("Ссылка на представление иерархии")]
    public HierarchyView hierarchyView;
    // Элементы UI
    private Label _stepLabel;
    private Label _partNameLabel;
    private Button _btnNext;
    private Button _btnPrev;
    private Button _btnReset;
    private Slider _transparencySlider;

    // Список всех деталей для расчета прозрачности
    private List<Renderer> _allRenderers = new List<Renderer>();

    // Текущая выбранная деталь (для обновления UI)
    private string _currentSelectedPartName = "-";

    private void Awake()
    {
        // Получаем UIDocument (предполагаем, что он на этом же объекте или ищем в сцене)
        UIDocument uiDocument = GetComponent<UIDocument>();
        if (uiDocument == null)
        {
            uiDocument = FindObjectOfType<UIDocument>();
        }

        if (uiDocument == null || uiDocument.rootVisualElement == null)
        {
            Debug.LogError("UIManager: Не найден UIDocument или корневой элемент!");
            return;
        }

        var root = uiDocument.rootVisualElement;

        // Привязка элементов по имени (должны совпадать с UXML)
        _stepLabel = root.Q<Label>("StepLabel");
        _partNameLabel = root.Q<Label>("PartNameLabel");
        _btnNext = root.Q<Button>("BtnNext");
        _btnPrev = root.Q<Button>("BtnPrev");
        _btnReset = root.Q<Button>("BtnReset");
        _transparencySlider = root.Q<Slider>("TransparencySlider");

        // Регистрация событий
        if (_btnNext != null) _btnNext.clicked += OnNextClicked;
        if (_btnPrev != null) _btnPrev.clicked += OnPrevClicked;
        if (_btnReset != null) _btnReset.clicked += OnResetClicked;
        
        if (_transparencySlider != null)
        {
            _transparencySlider.RegisterValueChangedCallback(OnTransparencyChanged);
            // Инициализация начального значения
            ApplyTransparency(_transparencySlider.value);
        }

        // Сбор всех рендереров для прозрачности
        var parts = FindObjectsOfType<PartController>();
        foreach (var part in parts)
        {
            var rend = part.GetComponent<Renderer>();
            if (rend != null) _allRenderers.Add(rend);
        }

        UpdateUI();
        
        Debug.Log("[UIManager] Инициализация завершена.");
    }

    private void OnNextClicked()
    {
        if (sequenceManager != null) sequenceManager.NextStep();
    }

    private void OnPrevClicked()
    {
        if (sequenceManager != null) sequenceManager.PreviousStep();
    }

    private void OnResetClicked()
    {
        if (sequenceManager != null) sequenceManager.ResetSequence();
    }

    private void OnTransparencyChanged(ChangeEvent<float> evt)
    {
        ApplyTransparency(evt.newValue);
    }

    /// <summary>
    /// Применяет прозрачность ко всем деталям через MaterialPropertyBlock
    /// </summary>
    private void ApplyTransparency(float alpha)
    {
        Color color = new Color(1, 1, 1, alpha);
        
        // Для стандартного шейдера нужно менять _Color и возможно режим рендеринга,
        // но для простого прототипа меняем только альфу цвета.
        // Внимание: Чтобы прозрачность работала визуально, материал должен поддерживать Transparent mode.
        // Если используется Def.mat (Opaque), альфа не сработает без смены шейдера.
        // Для прототипа: предполагаем, что материал поддерживает альфу или мы просто эмулируем логику.
        
        MaterialPropertyBlock mpb = new MaterialPropertyBlock();
        
        foreach (var renderer in _allRenderers)
        {
            renderer.GetPropertyBlock(mpb);
            // Получаем текущий цвет, чтобы не затереть RGB если они были изменены хайлайтером
            Color current = mpb.GetColor("_Color");
            current.a = alpha;
            mpb.SetColor("_Color", current);
            renderer.SetPropertyBlock(mpb);
        }
    }

    /// <summary>
    /// Обновление текста UI в зависимости от состояния SequenceManager
    /// Вызывать после каждого шага (можно подписаться на событие в SequenceManager или вызывать в Update)
    /// Для простоты вызовем в Update, так как это прототип.
    /// </summary>
    private void Update()
    {
        UpdateUI();
    }
        /// <summary>
    /// Публичный метод для обновления имени выбранной детали извне
    /// </summary>
    public void SetSelectedPartName(string name)
    {
        _currentSelectedPartName = !string.IsNullOrEmpty(name) ? name : "-";
    }

    private void UpdateUI()
    {
        if (_stepLabel == null) return;

        // Подсчет шагов
        int totalSteps = sequenceManager != null ? sequenceManager.allParts.Count : 0;
        int currentSteps = 0;
        string lastPartName = _currentSelectedPartName;

        if (sequenceManager != null)
        {
            foreach (var part in sequenceManager.allParts)
            {
                if (part.isDisassembled)
                {
                    currentSteps++;
                    // Обновляем имя последней разобранной детали, если не выбрано ничего другого
                    if (string.IsNullOrEmpty(_currentSelectedPartName) || _currentSelectedPartName == "-")
                    {
                        lastPartName = part.name;
                    }
                }
            }
        }

        _stepLabel.text = $"Шаг: {currentSteps} / {totalSteps}";
       _partNameLabel.text = $"Деталь: {lastPartName}";

        // Блокировка кнопок во время анимации
        bool isAnimating = sequenceManager != null && sequenceManager.isAnimating;
        
        if (_btnNext != null) _btnNext.SetEnabled(!isAnimating);
        if (_btnPrev != null) _btnPrev.SetEnabled(!isAnimating && currentSteps > 0);
        if (_btnReset != null) _btnReset.SetEnabled(!isAnimating && currentSteps > 0);
    }
}
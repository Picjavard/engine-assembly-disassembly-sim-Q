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
    private Toggle _enableTransparencyToggle;

    // Список всех деталей для расчета прозрачности
    private List<Renderer> _allRenderers = new List<Renderer>();

    // Уникальные материалы, которые надо переключать между Opaque/Transparent режимами.
    // Примечание: MaterialPropertyBlock не умеет менять Blend/ZWrite, поэтому нужен доступ к material.
    private readonly HashSet<Material> _materials = new HashSet<Material>();

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
        _enableTransparencyToggle = root.Q<Toggle>("EnableTransparencyToggle");

        // Регистрация событий
        if (_btnNext != null) _btnNext.clicked += OnNextClicked;
        if (_btnPrev != null) _btnPrev.clicked += OnPrevClicked;
        if (_btnReset != null) _btnReset.clicked += OnResetClicked;
        
        // Инициализация прозрачности
        bool transparencyEnabled = _enableTransparencyToggle != null ? _enableTransparencyToggle.value : true;
        if (_transparencySlider != null) 
        {
            _transparencySlider.RegisterValueChangedCallback(OnTransparencyChanged);
            _transparencySlider.SetEnabled(transparencyEnabled);

            // Изначально выставим режим материала и alpha
            // (после сборки _allRenderers и _materials мы вызовем ApplyTransparency еще раз).
        }

        if (_enableTransparencyToggle != null)
        {
            _enableTransparencyToggle.RegisterValueChangedCallback(OnTransparencyToggleChanged);
        }

        // Сбор всех рендереров для прозрачности
        var parts = FindObjectsOfType<PartController>();
        var uniqueRenderers = new HashSet<Renderer>();
        foreach (var part in parts)
        {
            // Часто меш лежит в дочерних объектах — берём все рендереры под деталью.
            var rends = part.GetComponentsInChildren<Renderer>(true);
            foreach (var rend in rends)
            {
                if (rend == null) continue;
                if (uniqueRenderers.Add(rend))
                {
                    _allRenderers.Add(rend);
                    if (rend != null && rend.sharedMaterial != null)
                    {
                        _materials.Add(rend.sharedMaterial);
                    }
                }
            }
        }

        // Применяем режим материала перед установкой alpha
        SetMaterialTransparencyMode(transparencyEnabled);
        ApplyTransparency(transparencyEnabled ? (_transparencySlider != null ? _transparencySlider.value : 1f) : 1f);

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
        // Если прозрачность выключена — игнорируем изменения слайдера.
        if (_enableTransparencyToggle != null && !_enableTransparencyToggle.value)
            return;
        ApplyTransparency(evt.newValue);
    }

    private void OnTransparencyToggleChanged(ChangeEvent<bool> evt)
    {
        bool enabled = evt.newValue;

        if (_transparencySlider != null)
            _transparencySlider.SetEnabled(enabled);

        // Переключаем Blend/ZWrite/keywords, чтобы включать/выключать "строгий Opaque".
        SetMaterialTransparencyMode(enabled);

        ApplyTransparency(enabled ? _transparencySlider.value : 1f);
    }

    /// <summary>
    /// Применяет прозрачность ко всем деталям через MaterialPropertyBlock
    /// </summary>
     private void ApplyTransparency(float alpha)
    {
        MaterialPropertyBlock mpb = new MaterialPropertyBlock();
        int colorId = Shader.PropertyToID("_Color");
        
        foreach (var renderer in _allRenderers)
        {
            if (renderer == null) continue;
            
            // 1. Получаем текущее состояние (из материала или предыдущего блока)
            renderer.GetPropertyBlock(mpb);
            
            // 2. Берем текущий цвет:
            //    - если _Color уже был переопределен в property block — используем его
            //    - иначе берем базовый цвет из sharedMaterial
            Color current;
            if (mpb != null && mpb.HasProperty(colorId))
            {
                current = mpb.GetColor(colorId);
            }
            else if (renderer.sharedMaterial != null && renderer.sharedMaterial.HasProperty("_Color"))
            {
                current = renderer.sharedMaterial.GetColor("_Color");
            }
            else
            {
                current = Color.white;
            }
            // 3. Меняем ТОЛЬКО альфу. Цвет (RGB) оставляем как есть (важно, если подсветка его как-то затронула, хотя мы так не делаем).
            current.a = alpha;
            
            // 4. Применяем обратно
            mpb.SetColor(colorId, current);
            renderer.SetPropertyBlock(mpb);
        }
    }

    private void SetMaterialTransparencyMode(bool enabled)
    {
        foreach (var mat in _materials)
        {
            if (mat == null) continue;

            // Убираем варианты, оставляем "обычный alpha blend"
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.DisableKeyword("_ALPHABLEND_ON");

            if (enabled)
            {
                mat.EnableKeyword("_ALPHABLEND_ON");

                // Standard transparent: SrcAlpha/OneMinusSrcAlpha, ZWrite Off
                if (mat.HasProperty("_Mode")) mat.SetFloat("_Mode", 3f);
                if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", 5f);
                if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", 10f);
                if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);

                mat.renderQueue = 3000;
            }
            else
            {
                // Оpaque: SrcAlpha(не важен) / OneMinusSrcAlpha(не важен), ZWrite On
                if (mat.HasProperty("_Mode")) mat.SetFloat("_Mode", 0f);
                if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", 1f);
                if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", 0f);
                if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 1f);

                mat.renderQueue = 2000;
            }
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
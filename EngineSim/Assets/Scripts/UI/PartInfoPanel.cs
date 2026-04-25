using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Правая панель с информацией о выбранной детали.
/// Отображает название и описание из JSON.
/// </summary>
public class PartInfoPanel : MonoBehaviour
{
    [Header("Элементы UI")]
    [Tooltip("Label для названия детали")]
    private Label _titleLabel;

    [Tooltip("Label для описания детали")]
    private Label _descriptionLabel;

    [Tooltip("VisualElement-контейнер панели (для анимаций)")]
    private VisualElement _panelContainer;

    // Цвета
    private readonly Color _defaultTitleColor = new Color(1f, 0.84f, 0f, 1f); // Золотистый
    private readonly Color _defaultTextColor = new Color(0.9f, 0.9f, 0.9f, 1f);

    private void Awake()
    {
        InitializeUI();
    }

    /// <summary>
    /// Инициализация UI элементов
    /// </summary>
    private void InitializeUI()
    {
        var uiDocument = GetComponent<UIDocument>();
        if (uiDocument == null)
        {
            uiDocument = FindObjectOfType<UIDocument>();
        }

        if (uiDocument == null || uiDocument.rootVisualElement == null)
        {
            Debug.LogError("[PartInfoPanel] UIDocument не найден!");
            return;
        }

        var root = uiDocument.rootVisualElement;

        // Находим элементы по именам (должны совпадать с UXML)
        _titleLabel = root.Q<Label>("InfoTitle");
        _descriptionLabel = root.Q<Label>("InfoDescription");
        _panelContainer = root.Q<VisualElement>("InfoPanelContainer");

        // Если элементы не найдены, создаем их программно (fallback)
        if (_titleLabel == null || _descriptionLabel == null)
        {
            Debug.LogWarning("[PartInfoPanel] Элементы не найдены в UXML. Создаем программно...");
            CreateFallbackUI(root);
        }
        else
        {
            // Устанавливаем начальные значения
            _titleLabel.text = "Информация о детали";
            _descriptionLabel.text = "Выберите деталь в дереве или кликните на 3D-модель.";

            // Применяем стили
            ApplyDefaultStyles();
        }

        Debug.Log("[PartInfoPanel] Панель информации инициализирована.");
    }

    /// <summary>
    /// Создание резервного UI, если элементы не найдены в UXML
    /// </summary>
    private void CreateFallbackUI(VisualElement root)
    {
        // Создаем контейнер
        _panelContainer = new VisualElement();
        _panelContainer.name = "InfoPanelContainer";
        _panelContainer.AddToClassList("info-panel-container");

        // Заголовок
        _titleLabel = new Label("Информация о детали");
        _titleLabel.name = "InfoTitle";
        _titleLabel.AddToClassList("info-title");

        // Описание
        _descriptionLabel = new Label("Выберите деталь в дереве или кликните на 3D-модель.");
        _descriptionLabel.name = "InfoDescription";
        _descriptionLabel.AddToClassList("info-description");

        // Добавляем в иерархию
        _panelContainer.Add(_titleLabel);
        _panelContainer.Add(_descriptionLabel);
        root.Add(_panelContainer);

        ApplyDefaultStyles();
    }

    /// <summary>
    /// Применение стилей по умолчанию
    /// </summary>
    private void ApplyDefaultStyles()
    {
        if (_titleLabel != null)
        {
            _titleLabel.style.fontSize = 20;
            _titleLabel.style.color = _defaultTitleColor;
            _titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        }

        if (_descriptionLabel != null)
        {
            _descriptionLabel.style.fontSize = 14;
            _descriptionLabel.style.color = _defaultTextColor;
            _descriptionLabel.style.whiteSpace = WhiteSpace.Normal;
        }
    }

    /// <summary>
    /// Обновление информации о детали
    /// Вызывается при клике в дереве или при выборе в 3D
    /// </summary>
    public void UpdateInfo(string title, string description)
    {
        if (_titleLabel != null)
        {
            _titleLabel.text = !string.IsNullOrEmpty(title) ? title : "Неизвестная деталь";
        }

        if (_descriptionLabel != null)
        {
            _descriptionLabel.text = !string.IsNullOrEmpty(description)
                ? description
                : "Описание отсутствует.";
        }

        // Анимация появления (опционально)
        FlashPanel();
    }

    /// <summary>
    /// Анимация вспышки панели при обновлении
    /// </summary>
    private void FlashPanel()
    {
        if (_panelContainer == null) return;

        // Простая анимация через DOTween
        DG.Tweening.DOTween.To(() => 1f, x => {
            if (_panelContainer != null)
            {
                _panelContainer.style.opacity = Mathf.Lerp(0.5f, 1f, x);
            }
        }, 0f, 0.3f);
    }

    /// <summary>
    /// Очистка информации (сброс к состоянию по умолчанию)
    /// </summary>
    public void ClearInfo()
    {
        UpdateInfo("Информация о детали", "Выберите деталь в дереве или кликните на 3D-модель.");
    }

    /// <summary>
    /// Показать/скрыть панель
    /// </summary>
    public void SetVisible(bool visible)
    {
        if (_panelContainer != null)
        {
            _panelContainer.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
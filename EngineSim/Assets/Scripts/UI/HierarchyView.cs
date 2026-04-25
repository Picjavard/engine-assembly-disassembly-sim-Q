using UnityEngine;
using UnityEngine.UIElements;
using AssemblyApp.Data;
using System.Collections.Generic;
using DG.Tweening;

/// <summary>
/// Отображает иерархию деталей в левой панели UI Toolkit.
/// Динамически генерирует дерево на основе данных из DataLoader.
/// </summary>
public class HierarchyView : MonoBehaviour
{
    [Header("Ссылки")]
    [Tooltip("Ссылка на загрузчик данных")]
    public DataLoader dataLoader;

    [Tooltip("Ссылка на контроллер камеры")]
    public OrbitCameraController cameraController;

    [Tooltip("Ссылка на менеджер выделения (для подсветки)")]
    public RaycastHighlighter highlighter;

    [Tooltip("Ссылка на панель информации (для обновления данных)")]
    public PartInfoPanel infoPanel;

    // Корневой элемент дерева
    private VisualElement _rootTree;

    // Словарь для быстрого доступа: ID узла -> Foldout
    private Dictionary<string, Foldout> _groupFoldouts;

    // Текущий выбранный объект
    private GameObject _selectedObject;

    // Цвета для состояний
    private readonly Color _defaultColor = new Color(0.8f, 0.8f, 0.8f, 1f);
    private readonly Color _selectedColor = new Color(1f, 0.84f, 0f, 1f); // Золотистый

    private void Awake()
    {
        _groupFoldouts = new Dictionary<string, Foldout>();
    }

    private void OnEnable()
    {
        BuildHierarchyTree();
    }

    /// <summary>
    /// Строит дерево иерархии на основе данных JSON
    /// </summary>
    public void BuildHierarchyTree()
    {
        if (dataLoader == null || dataLoader.Data == null)
        {
            Debug.LogError("[HierarchyView] DataLoader не найден или данные не загружены!");
            return;
        }

        var uiDocument = GetComponent<UIDocument>();
        if (uiDocument == null)
        {
            uiDocument = FindObjectOfType<UIDocument>();
        }

        if (uiDocument == null || uiDocument.rootVisualElement == null)
        {
            Debug.LogError("[HierarchyView] UIDocument не найден!");
            return;
        }

        var root = uiDocument.rootVisualElement;

        // Находим контейнер для дерева (должен быть в UXML)
        _rootTree = root.Q<VisualElement>("HierarchyTree");
        if (_rootTree == null)
        {
            Debug.LogError("[HierarchyView] Элемент 'HierarchyTree' не найден в UXML!");
            return;
        }

        // Очищаем существующее дерево
        _rootTree.Clear();
        _groupFoldouts.Clear();

        // Создаем заголовок
        var titleLabel = new Label(dataLoader.Data.rootName);
        titleLabel.AddToClassList("hierarchy-title");
        _rootTree.Add(titleLabel);

        // Строим дерево групп
        if (dataLoader.Data.groups != null)
        {
            foreach (var group in dataLoader.Data.groups)
            {
                CreateGroupElement(group);
            }
        }

        Debug.Log("[HierarchyView] Дерево иерархии построено.");
    }

    /// <summary>
    /// Создает элемент группы (Foldout)
    /// </summary>
    private void CreateGroupElement(AssemblyNode group)
    {
        if (group == null) return;

        // Создаем складываемый элемент для группы
        var foldout = new Foldout { text = group.name, value = true };
        foldout.AddToClassList("group-foldout");

        // Сохраняем ссылку для управления видимостью
        if (!_groupFoldouts.ContainsKey(group.id))
        {
            _groupFoldouts.Add(group.id, foldout);
        }

        // Если есть дочерние элементы, добавляем их
        if (group.children != null && group.children.Count > 0)
        {
            foreach (var child in group.children)
            {
                CreatePartElement(child, foldout);
            }
        }

        _rootTree.Add(foldout);
    }

    /// <summary>
    /// Создает элемент детали (Label с кликом)
    /// </summary>
    private void CreatePartElement(AssemblyNode partNode, VisualElement parent)
    {
        if (partNode == null) return;

        // Создаваем кликабельный элемент
        var label = new Label(partNode.name);
        label.AddToClassList("part-label");
        label.userData = partNode; // Сохраняем данные узла

        // Обработчик клика
        label.RegisterCallback<ClickEvent>(evt => OnPartClicked(partNode));

        // Обработчик наведения (для подсветки в 3D)
        label.RegisterCallback<MouseEnterEvent>(evt => OnPartHovered(partNode, true));
        label.RegisterCallback<MouseLeaveEvent>(evt => OnPartHovered(partNode, false));

        parent.Add(label);
    }

    /// <summary>
    /// Обработка клика по детали в дереве
    /// </summary>
    private void OnPartClicked(AssemblyNode node)
    {
        // Получаем GameObject по имени
        GameObject partObject = dataLoader.GetGameObjectByNode(node);

        if (partObject != null)
        {
            // Фокусировка камеры
            if (cameraController != null)
            {
                cameraController.FocusOnObject(partObject, 1.5f, 3f);
            }

            // Подсветка на 1 секунду
            HighlightPartTemporarily(partObject);

            // Выделение в дереве
            SelectPartInTree(node);

            // Обновление информационной панели
            if (infoPanel != null)
            {
                infoPanel.UpdateInfo(node.name, node.description);
            }

            _selectedObject = partObject;

            Debug.Log($"[HierarchyView] Выбрана деталь: {node.name}");
        }
        else
        {
            Debug.LogWarning($"[HierarchyView] Объект '{node.objectName}' не найден в сцене!");
        }
    }

    /// <summary>
    /// Временная подсветка детали при выборе в дереве
    /// </summary>
    private void HighlightPartTemporarily(GameObject target)
    {
        if (highlighter == null || target == null) return;

        // Применяем подсветку через MaterialPropertyBlock
        Renderer renderer = target.GetComponent<Renderer>();
        if (renderer != null)
        {
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);

            // Устанавливаем цвет эмиссии (золотистый)
            Color highlightColor = new Color(1f, 0.8f, 0f, 1f) * 0.8f;
            int emissionId = Shader.PropertyToID("_EmissionColor");
            block.SetColor(emissionId, highlightColor);

            renderer.SetPropertyBlock(block);

            // Сброс подсветки через 1 секунду с помощью DOTween
            DG.Tweening.DOTween.To(() => 1f, x => {
                if (renderer != null)
                {
                    renderer.GetPropertyBlock(block);
                    Color fadingColor = highlightColor * (1f - x);
                    block.SetColor(emissionId, fadingColor);
                    renderer.SetPropertyBlock(block);
                }
            }, 0f, 2f)
            .SetEase(Ease.InOutQuad)
            .OnComplete(() => {
                // Полностью убираем подсветку после завершения анимации
                if (renderer != null)
                {
                    MaterialPropertyBlock clearBlock = new MaterialPropertyBlock();
                    renderer.SetPropertyBlock(clearBlock);
                }
            });
        }
    }

    /// <summary>
    /// Выделение элемента в дереве (визуальное)
    /// </summary>
    private void SelectPartInTree(AssemblyNode selectedNode)
    {
        // Сбрасываем предыдущее выделение
        var allLabels = _rootTree.Query<Label>("part-label").ToList();
        foreach (var label in allLabels)
        {
            label.RemoveFromClassList("part-selected");
        }

        // Находим и выделяем текущий элемент
        foreach (var label in allLabels)
        {
            if (label.userData is AssemblyNode node && node.id == selectedNode.id)
            {
                label.AddToClassList("part-selected");
                break;
            }
        }
    }

    /// <summary>
    /// Обработка наведения на элемент дерева
    /// </summary>
    private void OnPartHovered(AssemblyNode node, bool isHovering)
    {
        if (!isHovering) return;

        // Можно добавить предварительную подсветку или тултип
        // Для сейчас просто логируем
        // Debug.Log($"[HierarchyView] Наведение на: {node.name}");
    }

    /// <summary>
    /// Публичный метод для выбора детали извне (например, из InteractionController)
    /// </summary>
    public void SelectPartByNode(AssemblyNode node)
    {
        if (node != null)
        {
            OnPartClicked(node);
        }
    }

    /// <summary>
    /// Получить текущий выбранный объект
    /// </summary>
    public GameObject GetSelectedObject()
    {
        return _selectedObject;
    }
}
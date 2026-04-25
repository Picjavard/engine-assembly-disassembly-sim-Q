using UnityEngine;
using DG.Tweening;
using System.Collections;

/// <summary>
/// Отвечает за подсветку деталей при наведении и выделении.
/// Использует MaterialPropertyBlock для производительности (без клонирования материалов).
/// </summary>
public class RaycastHighlighter : MonoBehaviour
{
    [Header("Настройки подсветки")]
    [ColorUsage(true, true)]
    public Color hoverColor = new Color(1f, 0.8f, 0.2f, 1f); // Золотистый для наведения
    [ColorUsage(true, true)]
    public Color selectColor = new Color(1f, 0.9f, 0.4f, 1.5f); // Более яркий для выбора
    [ColorUsage(true, true)]
    public Color errorColor = new Color(1f, 0.2f, 0.2f, 1.5f); // Красный для ошибки

    public float hoverIntensity = 0.5f;
    public float selectIntensity = 1.5f;
    public float errorIntensity = 2.0f;
    
    [Range(0.1f, 2f)]
    public float animationDuration = 0.5f; // Длительность плавного перехода

    private Renderer _objectRenderer;
    private MaterialPropertyBlock _propertyBlock;
    private static readonly int EmissionMapID = Shader.PropertyToID("_EmissionColor");
    
    // Флаги состояния
    private bool _isHovering = false;
    private bool _isSelected = false;
    
    // Текущая целевая интенсивность и цвет
    private Color _targetColor = Color.black;
    private float _targetIntensity = 0f;

    // Для отмены текущей анимации
    private Tween _currentTween;

    private void Awake()
    {
        _objectRenderer = GetComponent<Renderer>();
        if (_objectRenderer == null)
        {
            Debug.LogWarning($"[RaycastHighlighter] На объекте {gameObject.name} нет Renderer!");
            enabled = false;
            return;
        }
        
        _propertyBlock = new MaterialPropertyBlock();
        // Инициализация нулевым значением
        _objectRenderer.GetPropertyBlock(_propertyBlock);
        _propertyBlock.SetColor(EmissionMapID, Color.black);
        _objectRenderer.SetPropertyBlock(_propertyBlock);
    }

    /// <summary>
    /// Вызывается при наведении мыши (из InteractionController или через события Unity)
    /// </summary>
    public void OnHoverEnter()
    {
        if (_isSelected) return; // Если уже выделено, ховер не меняем
        
        _isHovering = true;
        UpdateTargetState();
    }

    /// <summary>
    /// Вызывается при уходе мыши
    /// </summary>
    public void OnHoverExit()
    {
        if (_isSelected) return; // Если выделено, уход мыши не сбрасывает подсветку
        
        _isHovering = false;
        UpdateTargetState();
    }

    /// <summary>
    /// Явное выделение объекта (клик ЛКМ или выбор в иерархии)
    /// </summary>
    public void Select()
    {
        _isSelected = true;
        _isHovering = false; // При выделении ховер больше не важен
        UpdateTargetState();
    }

    /// <summary>
    /// Снять выделение
    /// </summary>
    public void Deselect()
    {
        _isSelected = false;
        UpdateTargetState();
    }

    /// <summary>
    /// Временная подсветка ошибки (мигание красным)
    /// </summary>
    public void FlashError()
    {
        // Прерываем текущую анимацию обычного состояния
        if (_currentTween != null && _currentTween.IsActive())
            _currentTween.Kill();

        // Запоминаем состояние, чтобы вернуться к нему
        Color originalTarget = _targetColor;
        float originalIntensity = _targetIntensity;

        // Анимация ошибки: резко включаем красный, потом плавно гасим
        _propertyBlock.SetColor(EmissionMapID, errorColor * errorIntensity);
        _objectRenderer.SetPropertyBlock(_propertyBlock);

        // Возврат к предыдущему состоянию через 0.5 сек
        DOVirtual.DelayedCall(animationDuration, () =>
        {
            // Восстанавливаем логику целевого состояния
            UpdateTargetState();
        });
    }

    /// <summary>
    /// Вычисляет целевое состояние на основе флагов
    /// </summary>
    private void UpdateTargetState()
    {
        if (_isSelected)
        {
            _targetColor = selectColor;
            _targetIntensity = selectIntensity;
        }
        else if (_isHovering)
        {
            _targetColor = hoverColor;
            _targetIntensity = hoverIntensity;
        }
        else
        {
            _targetColor = Color.black;
            _targetIntensity = 0f;
        }

        StartAnimation();
    }

    private void StartAnimation()
    {
        if (_currentTween != null && _currentTween.IsActive())
            _currentTween.Kill();

        // Получаем текущее значение из блока свойств (чтобы продолжать с места остановки)
        _objectRenderer.GetPropertyBlock(_propertyBlock);
        Color currentColor = _propertyBlock.GetColor(EmissionMapID);

        // Используем DOTween для плавной интерполяции цвета
        // Мы анимируем "фиктивный" объект или просто используем Callback
        _currentTween = DOVirtual.Color(
            currentColor, 
            _targetColor * _targetIntensity, 
            animationDuration, 
            OnColorUpdate
        ).SetEase(Ease.InOutSine); // Плавное нарастание и затухание
    }

    private void OnColorUpdate(Color value)
    {
        if (_objectRenderer == null) return;
        
        _propertyBlock.SetColor(EmissionMapID, value);
        _objectRenderer.SetPropertyBlock(_propertyBlock);
    }

    private void OnDestroy()
    {
        if (_currentTween != null && _currentTween.IsActive())
            _currentTween.Kill();
    }
}
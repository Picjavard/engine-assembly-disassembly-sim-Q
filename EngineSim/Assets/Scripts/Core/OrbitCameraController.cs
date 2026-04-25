using UnityEngine;

/// <summary>
/// Контроллер орбитальной камеры для вращения и зума вокруг точки интереса.
/// Не использует Cinemachine для максимальной производительности и простоты.
/// </summary>
[RequireComponent(typeof(Camera))]
public class OrbitCameraController : MonoBehaviour
{
    [Header("Настройки цели")]
    [Tooltip("Точка, вокруг которой вращается камера. Если null, используется (0,0,0).")]
    [SerializeField] private Transform _target;

    [Header("Параметры вращения")]
    [Tooltip("Скорость вращения камеры мышью.")]
    [SerializeField] private float _rotationSpeed = 120f;
    [Tooltip("Минимальный угол возвышения камеры (защита от переворота вниз).")]
    [SerializeField] private float _minVerticalAngle = -80f;
    [Tooltip("Максимальный угол возвышения камеры.")]
    [SerializeField] private float _maxVerticalAngle = 80f;

    [Header("Параметры зума")]
    [Tooltip("Скорость приближения/удаления колесиком мыши.")]
    [SerializeField] private float _zoomSpeed = 5f;
    [Tooltip("Минимальное расстояние до цели.")]
    [SerializeField] private float _minDistance = 2f;
    [Tooltip("Максимальное расстояние до цели.")]
    [SerializeField] private float _maxDistance = 20f;
    [Tooltip("Текущее расстояние (начальное значение).")]
    [SerializeField] private float _currentDistance = 10f;

    [Header("Настройки инерции (опционально)")]
    [Tooltip("Плавность следования камеры (0 = мгновенно, 1 = очень медленно).")]
    [Range(0f, 0.95f)]
    [SerializeField] private float _smoothness = 0.1f;

    // Внутренние переменные состояния
    private Vector3 _currentTargetPosition;
    private float _horizontalAngle;
    private float _verticalAngle;
    private float _targetDistance;
    
    // Ссылка на трансформ для кэширования
    private Transform _cameraTransform;

    private void Awake()
    {
        _cameraTransform = transform;
        
        // Инициализация углов на основе текущего положения камеры, 
        // чтобы не было скачка при старте
        InitializeAngles();
    }

    private void Start()
    {
        if (_target == null)
        {
            // Создаем пустой объект в центре, если цель не задана
            GameObject centerPivot = new GameObject("CameraPivot");
            centerPivot.transform.position = Vector3.zero;
            _target = centerPivot.transform;
        }
        
        _currentTargetPosition = _target.position;
        _targetDistance = Mathf.Clamp(_currentDistance, _minDistance, _maxDistance);
    }

    /// <summary>
    /// Вычисляет начальные углы сферических координат на основе позиции камеры в редакторе.
    /// </summary>
    private void InitializeAngles()
    {
        if (_target == null) return;

        Vector3 direction = _cameraTransform.position - _target.position;
        
        // Горизонтальный угол (вокруг оси Y)
        _horizontalAngle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
        
        // Вертикальный угол (вверх-вниз)
        float horizontalDist = new Vector3(direction.x, 0, direction.z).magnitude;
        _verticalAngle = Mathf.Atan2(direction.y, horizontalDist) * Mathf.Rad2Deg;
    }

    private void LateUpdate()
    {
        // Обновляем позицию цели на случай, если она двигается (хотя в разборе обычно статична)
        if (_target != null)
        {
            _currentTargetPosition = _target.position;
        }

        HandleInput();
        UpdateCameraPosition();
    }

    /// <summary>
    /// Обработка ввода пользователя (Мышь).
    /// </summary>
    private void HandleInput()
    {
        // Вращение: Левая кнопка мыши (или перетаскивание)
        // Используем GetMouseButton для постоянного вращения при зажатии
        if (Input.GetMouseButton(0)) 
        {
            float mouseX = Input.GetAxis("Mouse X");
            float mouseY = Input.GetAxis("Mouse Y");

            _horizontalAngle += mouseX * _rotationSpeed * Time.deltaTime;
            _verticalAngle -= mouseY * _rotationSpeed * Time.deltaTime;

            // Ограничение вертикального угла (чтобы камера не ушла под землю или не перевернулась)
            _verticalAngle = Mathf.Clamp(_verticalAngle, _minVerticalAngle, _maxVerticalAngle);
        }

        // Зум: Колесико мыши
        float scrollWheel = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scrollWheel) > 0.01f)
        {
            _targetDistance -= scrollWheel * _zoomSpeed * 10f; // Умножаем на 10 для чувствительности
            _targetDistance = Mathf.Clamp(_targetDistance, _minDistance, _maxDistance);
        }
    }

    /// <summary>
    /// Применяет вычисленные сферические координаты к позиции камеры.
    /// </summary>
    private void UpdateCameraPosition()
    {
        // Плавное изменение дистанции (Lerp)
        float currentDist = Mathf.Lerp(_cameraTransform.localPosition.magnitude, _targetDistance, 1f - _smoothness);
        
        // Пересчет текущей дистанции с учетом плавности, если нужно строго следовать за целевой
        // Для простоты прототипа используем прямое присваивание с небольшим сглаживанием позиции
        
        // Конвертация сферических координат в декартовы
        float radH = _horizontalAngle * Mathf.Deg2Rad;
        float radV = _verticalAngle * Mathf.Deg2Rad;

        Vector3 offset = Vector3.zero;
        offset.x = currentDist * Mathf.Sin(radH) * Mathf.Cos(radV);
        offset.y = currentDist * Mathf.Sin(radV);
        offset.z = currentDist * Mathf.Cos(radH) * Mathf.Cos(radV);

        Vector3 desiredPosition = _currentTargetPosition + offset;

        // Плавное движение самой камеры к нужной точке
        _cameraTransform.position = Vector3.Lerp(_cameraTransform.position, desiredPosition, 1f - _smoothness);
        
        // Камера всегда смотрит на цель
        _cameraTransform.LookAt(_currentTargetPosition);
    }

    /// <summary>
    /// Публичный метод для сброса камеры (можно вызвать из UI).
    /// </summary>
    public void ResetCamera()
    {
        _horizontalAngle = 0f;
        _verticalAngle = 20f; // Немного сверху
        _targetDistance = 10f;
    }
    
    /// <summary>
    /// Установка новой цели для камеры (если модель смещена).
    /// </summary>
    public void SetTarget(Transform newTarget)
    {
        _target = newTarget;
        if(_target != null) _currentTargetPosition = _target.position;
    }
}
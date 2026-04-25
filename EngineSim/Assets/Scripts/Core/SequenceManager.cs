using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

/// <summary>
/// Управляет последовательностью шагов разборки и сборки.
/// Блокирует ввод во время анимации, обрабатывает откат назад.
/// </summary>
public class SequenceManager : MonoBehaviour
{
    [Header("Настройки сцены")]
    [Tooltip("Список всех контроллеров деталей в сцене. Заполняется автоматически или вручную.")]
    public List<PartController> allParts = new List<PartController>();

    [Header("Параметры анимации")]
    [Tooltip("Общая длительность анимации (если не указана в шаге)")]
    public float defaultDuration = 1.0f;

    [Tooltip("Тип плавности анимации")]
    public Ease easeType = Ease.InOutQuad;

   
    [Tooltip("Блокировка ввода во время анимации")]
    public bool isAnimating { get; private set; } = false;

    // Ссылка на текущую активную твин-последовательность
    private Sequence _currentSequence;

    private void Awake()
    {
        // Если список пуст, пытаемся найти все PartController в сцене автоматически
        if (allParts.Count == 0)
        {
            allParts = new List<PartController>(FindObjectsOfType<PartController>());
            Debug.Log($"[SequenceManager] Найдено деталей автоматически: {allParts.Count}");
        }

        // Инициализируем DOTween. 
        // Вызов Init безопасен даже если уже был вызван ранее.
        // Параметры: recycleAllByDefault, useSafeMode, logBehaviour
        DOTween.Init(false, false, LogBehaviour.ErrorsOnly);
        
        // Устанавливаем целевой FPS для плавности (опционально)
        //DOTween.SetTargetFPS(60);
    }

    /// <summary>
    /// Запуск процесса разборки (выполняет следующий шаг)
    /// </summary>
    public void NextStep()
    {
        if (isAnimating) return;

        PartController nextPart = null;
        DisassemblyStep stepData = null;

        // Ищем первую еще не разобранную деталь
        foreach (var part in allParts)
        {
            if (!part.isDisassembled && part.associatedStep != null)
            {
                nextPart = part;
                stepData = part.associatedStep;
                break;
            }
        }

        if (nextPart != null && stepData != null)
        {
            ExecuteStep(nextPart, stepData, true);
        }
        else
        {
            Debug.Log("[SequenceManager] Все детали уже разобраны.");
        }
    }

    /// <summary>
    /// Запуск процесса сборки (откат последнего шага)
    /// </summary>
    public void PreviousStep()
    {
        if (isAnimating) return;

        PartController lastPart = null;
        DisassemblyStep stepData = null;

        // Идем с конца списка, чтобы найти последнюю разобранную деталь
        for (int i = allParts.Count - 1; i >= 0; i--)
        {
            if (allParts[i].isDisassembled && allParts[i].associatedStep != null)
            {
                lastPart = allParts[i];
                stepData = allParts[i].associatedStep;
                break;
            }
        }

        if (lastPart != null && stepData != null)
        {
            ExecuteStep(lastPart, stepData, false);
        }
        else
        {
            Debug.Log("[SequenceManager] Все детали уже собраны.");
        }
    }

    /// <summary>
    /// Выполнение одного шага (разборка или сборка)
    /// </summary>
    private void ExecuteStep(PartController part, DisassemblyStep data, bool disassemble)
    {
        isAnimating = true;
        
        float duration = data.duration > 0 ? data.duration : defaultDuration;
        Transform targetTransform = part.transform;

        // Определяем целевые значения
        Vector3 targetPos = disassemble ? data.targetPosition : part.initialLocalPosition;
        Vector3 targetRot = disassemble ? data.targetRotation : part.initialLocalRotation;

        // Создаем последовательность
        _currentSequence = DOTween.Sequence();
        
        // Добавляем задержку, если указана в шаге
        if (data.delay > 0)
        {
            _currentSequence.AppendInterval(data.delay);
        }

        // Добавляем анимацию позиции (Локальная)
        _currentSequence.Join(targetTransform.DOLocalMove(targetPos, duration).SetEase(easeType));

        // Добавляем анимацию вращения (Локальная)
        _currentSequence.Join(targetTransform.DOLocalRotate(targetRot, duration).SetEase(easeType));

        // Завершение последовательности
        _currentSequence.OnComplete(() =>
        {
            isAnimating = false;
            part.SetState(disassemble);
            
            if (disassemble && data.isRemovalStep)
            {
                // Пример логики для полного удаления: отключить коллайдер
                Collider col = part.GetComponent<Collider>();
                if(col != null) col.enabled = false;
            }
            
            Debug.Log($"[SequenceManager] Шаг завершен. Разобрано: {disassemble}");
        });

        _currentSequence.Play();
    }
    
    /// <summary>
    /// Полная перезагрузка состояния (сброс всех деталей)
    /// </summary>
    public void ResetSequence()
    {
        if (_currentSequence != null && _currentSequence.IsActive())
            _currentSequence.Kill();
            
        foreach (var part in allParts)
        {
            part.ResetState(); // Используем метод сброса из PartController
        }
        isAnimating = false;
        Debug.Log("[SequenceManager] Последовательность сброшена.");
    }
}
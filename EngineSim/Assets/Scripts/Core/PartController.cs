using UnityEngine;

/// <summary>
/// Компонент отдельной детали модели.
/// </summary>
public class PartController : MonoBehaviour
{
    [Header("Настройки связи")]
    [Tooltip("Шаг разборки, к которому относится эта деталь.")]
    public DisassemblyStep associatedStep;

    [Header("Состояние")]
    public bool isDisassembled = false;

    // Скрытые поля для хранения начального состояния
    [HideInInspector] public Vector3 initialLocalPosition;
    [HideInInspector] public Vector3 initialLocalRotation;

    private void Awake()
    {
        // Запоминаем, где деталь стояла в начале сцены
        initialLocalPosition = transform.localPosition;
        initialLocalRotation = transform.localEulerAngles;
    }

    public void ResetState()
    {
        isDisassembled = false;
        transform.localPosition = initialLocalPosition;
        transform.localRotation = Quaternion.Euler(initialLocalRotation);
    }

    public void SetState(bool disassembled)
    {
        isDisassembled = disassembled;
    }
}
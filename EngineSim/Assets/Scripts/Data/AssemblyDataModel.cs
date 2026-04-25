using System;
using System.Collections.Generic;
using UnityEngine;

namespace AssemblyApp.Data
{
    /// <summary>
    /// Модель одного узла дерева (группа или деталь)
    /// </summary>
    [Serializable]
    public class AssemblyNode
    {
        public string id;
        public string name;
        public string objectName; // Имя GameObject в сцене (для поиска)
        public string stepAssetName; // Имя ассета шага (для загрузки DisassemblyStep)
        public string description; // Текст для правой панели
        public List<AssemblyNode> children; // Вложенные элементы (если это группа)
        
        public bool IsGroup => children != null && children.Count > 0;
    }

    /// <summary>
    /// Корневая модель всего JSON файла
    /// </summary>
    [Serializable]
    public class AssemblyDataRoot
    {
        public string rootName;
        public List<AssemblyNode> groups;
    }
}
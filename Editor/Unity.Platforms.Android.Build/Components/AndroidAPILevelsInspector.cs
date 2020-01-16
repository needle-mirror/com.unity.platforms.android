using System.Linq;
using JetBrains.Annotations;
using Unity.Properties.Editor;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Unity.Platforms.Android.Build
{
    [UsedImplicitly]
    sealed class AndroidAPILevelsInspector : IInspector<AndroidAPILevels>
    {
        PopupField<int> m_TargetApiPopup;
        
        public VisualElement Build(InspectorContext<AndroidAPILevels> context)
        {
            var root = new VisualElement();

            var minApiPopup = new PopupField<int>(
                ObjectNames.NicifyVariableName(nameof(AndroidAPILevels.MinAPILevel)),
                AndroidAPILevels.s_AndroidCodeNames.Keys.ToList(),
                0,
                value => AndroidAPILevels.s_AndroidCodeNames[value],
                value => AndroidAPILevels.s_AndroidCodeNames[value])
            {
                bindingPath = nameof(AndroidAPILevels.MinAPILevel)
            };
            root.contentContainer.Add(minApiPopup);

            m_TargetApiPopup = new PopupField<int>(
                ObjectNames.NicifyVariableName(nameof(AndroidAPILevels.TargetAPILevel)),
                AndroidAPILevels.s_AndroidCodeNames.Keys.ToList(),
                0,
                value => AndroidAPILevels.s_AndroidCodeNames[value],
                value => AndroidAPILevels.s_AndroidCodeNames[value])
            {
                bindingPath = nameof(AndroidAPILevels.TargetAPILevel)
            };
            root.contentContainer.Add(m_TargetApiPopup);

            return root;
        }

        public void Update(InspectorContext<AndroidAPILevels> context)
        {
            // Nothing to do.
        }
    }
}
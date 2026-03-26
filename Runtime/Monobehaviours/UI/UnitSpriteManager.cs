using UnityEngine;
using UnityEngine.UIElements;

namespace Monobehaviours.UI
{
    public class UnitSpriteManager : MonoBehaviour
    {
        public UIDocument spriteDoc;
        private Label NumberLabel;
        private Slider StgthSlider;
        private Slider OrgSlider;
        public void OnEnable()
        {
            var root = spriteDoc.rootVisualElement;
            NumberLabel = root.Q<Label>("BattallionCount");
            StgthSlider = root.Q<Slider>("StrengthSlider");
            OrgSlider = root.Q<Slider>("OrgSlider");
        }

        public void UpdateBattalionInfo(int count, float strength, float org)
        {
            NumberLabel.text = count.ToString();
            StgthSlider.value = strength * 100;
            OrgSlider.value = org * 100;
        }
    }
}
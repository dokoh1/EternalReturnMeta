namespace Fusion.Menu
{
    using UnityEngine;
    using UnityEngine.UI;

    [RequireComponent(typeof(Button))]
    public class FusionMenuScreenPluginTooltip : FusionMenuScreenPlugin
    {
        [SerializeField] protected string _header;
        [SerializeField, TextArea] protected string _tooltip;
        [SerializeField] protected Button _button;

        private IFusionMenuUIController _controller;

        public virtual void Awake()
        {
            _button.onClick.AddListener(() => _controller.Popup(_tooltip, _header));
        }
        
        public override void Show(FusionMenuUIScreen screen)
        {
            base.Show(screen);
            _controller = screen.Controller;
        }

        public override void Hide(FusionMenuUIScreen screen)
        {
            base.Hide(screen);
            _controller = null;
        }
        
    }
}
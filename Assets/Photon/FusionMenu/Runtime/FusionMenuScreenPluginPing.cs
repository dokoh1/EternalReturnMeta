namespace Fusion.Menu
{
    using System;
#if FUSION_ENABLE_TEXTMESHPRO
    using Text = TMPro.TMP_text;
#else
    using Text = UnityEngine.UI.Text;
#endif
    using UnityEngine;
    using UnityEngine.UI;

    public class FusionMenuScreenPluginPing : FusionMenuScreenPlugin
    {
        [Serializable]
        public struct ColorThresholds
        {
            public int MaxPing;
            public Color Color;
        }
        [SerializeField] protected Text _pingText;
        [SerializeField] protected Image _coloredImage;
        [SerializeField] protected ColorThresholds[] _colorThresholds;
        private IFusionMenuConnection _connection;

        public override void Show(FusionMenuUIScreen screen)
        {
            base.Show(screen);
            
            _connection = screen.Connection;
        }

        public override void Hide(FusionMenuUIScreen screen)
        {
            base.Hide(screen);
            _connection = null;
        }

        public virtual void Update()
        {
            if (_connection == null)
                return;
            if (_pingText != null)
                _pingText.text = _connection.Ping.ToString();
            if (_coloredImage != null)
            {
                for (int i = 0; i < _colorThresholds.Length; i++)
                {
                    if (_connection.Ping <= _colorThresholds[i].MaxPing || i == _colorThresholds.Length - 1)
                    {
                        if (_coloredImage.color != _colorThresholds[i].Color)
                        {
                            _coloredImage.color = _colorThresholds[i].Color;
                        }

                        break;
                    }
                }
            }
        }
    }
}
namespace Fusion.Menu {
#if FUSION_ENABLE_TEXTMESHPRO
  using Text = TMPro.TMP_Text;
#else 
    using Text = UnityEngine.UI.Text;
#endif
    using UnityEngine;
    // 화면에 실시간으로 FPS를 추정해서 화면에 실시간으로 평균 FPS 값을 보여주는 UI 구성 요소
    public class FusionMenuFpsAvgCounter : FusionMenuScreenPlugin {

        [InlineHelp, SerializeField] protected Text _fpsText;

        private float[] _duration = new float[60];
        private int _index;
        private int _samples;


        public virtual void Update() {

            _samples = _samples++ % _duration.Length;
            if (_samples % 2 != 0) {
                return;
            }

            _duration[_index++] = Time.unscaledDeltaTime;
            _index = _index % _duration.Length;

            var accum = 0.0f;
            var count = 0;
            for (int i = 0; i < _duration.Length; i++) {
                if (_duration[i] > 0.0f) {
                    accum += _duration[i];
                    count++;
                }
            }

            var fps = 0;
            if (count > 0) {
                fps = (int)(count / accum);
            }

            _fpsText.text = fps.ToString();
        }
    }
}
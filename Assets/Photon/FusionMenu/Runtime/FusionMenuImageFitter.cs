namespace Fusion.Menu
{
    using UnityEngine;
    using UnityEngine.UI;
    [RequireComponent(typeof(Image))]
    [RequireComponent(typeof(RectTransform))]
    //이미지의 종횡비 유지하며 부모 크기에 맞춰 자동 조절
    // 배경, 썸네일 이미지, 화면 전환 시 등장하는 풀 스크린 이미지에만 적용
    public class FusionMenuImageFitter : MonoBehaviour
    {
        private Image _image;
        private RectTransform _parentTransform;
        private RectTransform _rectTransform;
        private Vector2 _resolution;

        public void Awake()
        {
            _image = GetComponent<Image>();
            _parentTransform = transform.parent.GetComponent<RectTransform>();
            _rectTransform = transform.GetComponent<RectTransform>();
        }

        public void OnresolutionChanged()
        {
            CalculateAspect();
        }

        public void Start()
        {
            CalculateAspect();
        }

        public void Update()
        {
            if (_resolution.x != _parentTransform.rect.width ||
                _resolution.y != _parentTransform.rect.height)
            {
                _resolution.x = _parentTransform.rect.width;
                _resolution.y = _parentTransform.rect.height;
                CalculateAspect();
            }
        }
        
        private void CalculateAspect() {
            if (_image.sprite == null) {
                return;
            }

            var parentAspect = _parentTransform.rect.width / _parentTransform.rect.height;
            var spriteAspect = _image.sprite.rect.width / _image.sprite.rect.height;

            if (spriteAspect >= parentAspect) {
                var a = _parentTransform.rect.height / _image.sprite.rect.height;
                var w = a * _image.sprite.rect.width;
                _rectTransform.sizeDelta = new Vector2(w, _parentTransform.rect.height);
            } else {
                var a = _parentTransform.rect.width / _image.sprite.rect.width;
                var h = a * _image.sprite.rect.height;
                _rectTransform.sizeDelta = new Vector2(_parentTransform.rect.width, h);
            }
        }
    }
    
}

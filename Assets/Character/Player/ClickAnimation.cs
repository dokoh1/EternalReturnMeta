using UnityEngine;
using DG.Tweening;

public class ClickAnimation : MonoBehaviour
{
    private ParticleSystem particleSystem;
    private Material particleMaterial;
    private Material[] childMaterials;

    private void Start()
    {
        // 부모의 ParticleSystem 초기화
        InitParticleSystem();

        // 자식 오브젝트의 Material 초기화
        InitChildMaterials();

        // 부모의 Material 투명도 애니메이션
        if (particleMaterial != null)
        {
            Color startColor = particleMaterial.color;
            particleMaterial.DOColor(new Color(startColor.r, startColor.g, startColor.b, 0f), "_Color", 1f);
        }

        // 자식 오브젝트의 Material 색상 애니메이션
        foreach (Material mat in childMaterials)
        {
            if (mat != null)
            {
                Color startColor = mat.color;
                mat.DOColor(new Color(startColor.r, startColor.g, startColor.b, 0f), "_Color", 1f);
            }
        }
    }

    private void InitParticleSystem()
    {
        particleSystem = GetComponent<ParticleSystem>();
        if (particleSystem != null && particleSystem.GetComponent<Renderer>() != null)
        {
            particleMaterial = particleSystem.GetComponent<Renderer>().material;
        }
    }

    private void InitChildMaterials()
    {
        Renderer[] childRenderers = GetComponentsInChildren<Renderer>(); // 모든 자식의 Renderer 가져오기
        childMaterials = new Material[childRenderers.Length];

        for (int i = 0; i < childRenderers.Length; i++)
        {
            childMaterials[i] = childRenderers[i].material;
        }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

public class GPUSkinningSetter : MonoBehaviour
{
    [FormerlySerializedAs("animation")]
    public GPUSkinningAsset animSkinningAsset;
    private MeshRenderer render;
    private MaterialPropertyBlock matBlock;
    private Material mat;
    private Texture2DArray texArray;

    void Start()
    {
        SetInfo();
    }

    private void OnEnable()
    {
        SetInfo();
    }

    private void SetInfo()
    {
        if (render == null) render = gameObject.GetComponent<MeshRenderer>();
        if (matBlock == null) matBlock = new MaterialPropertyBlock();
        if (animSkinningAsset != null)
        {
            CopyTextureSupport copyTextureSupport = SystemInfo.copyTextureSupport;
            texArray = new Texture2DArray(animSkinningAsset.textureSize.x, animSkinningAsset.textureSize.y, animSkinningAsset.textures.Length, animSkinningAsset.textures[0].format, false, false);
            texArray.filterMode = FilterMode.Point;
            DontDestroyOnLoad(texArray);
            for (int i = 0; i < animSkinningAsset.textures.Length; ++i)
            {
                if (copyTextureSupport == UnityEngine.Rendering.CopyTextureSupport.None)
                    texArray.SetPixels(animSkinningAsset.textures[i].GetPixels(0),i,0);
                else
                    Graphics.CopyTexture(animSkinningAsset.textures[i],0,0,texArray,i,0);
                mat = render.sharedMaterial;
                mat.SetTexture("_AnimationTex",texArray);
                if (copyTextureSupport == UnityEngine.Rendering.CopyTextureSupport.None)
                    texArray.Apply();
                render.GetPropertyBlock(matBlock);
                matBlock.SetVector("_Scale",animSkinningAsset.animScalar);
                matBlock.SetFloat("_AnimationSize",animSkinningAsset.animTime);
                matBlock.SetInt("_FPS",60);
                matBlock.SetInt("_VertexNum",animSkinningAsset.vertexCount);
                matBlock.SetVector("_TextureSize",new Vector4(animSkinningAsset.textureSize.x,animSkinningAsset.textureSize.y,0,0));
                render.SetPropertyBlock(matBlock);
            }
        }

    }
}

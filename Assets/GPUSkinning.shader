
Shader "Unlit/GPUSkinning"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _AnimationTex ("Texture", 2DArray) = ""{}             //动画贴图
        _Scale ("Scale", Vector) = (1, 1, 1, 0)             //x, y, z轴的缩放
        _AnimationSize ("Animation Size", float) = 0        //动画长度
        _FPS("FPS", Int) = 0                                //FPS
        _VertexNum("Vertex Num", Int) = 0                   //顶点数
        _TextureSize("Texture Size", Vector) = (0, 0, 0, 0) //动画贴图大小
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma require 2darray
            #pragma multi_compile_instancing
            #pragma target 3.5

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                uint vertexId : SV_VertexID;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float4 color : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            UNITY_DECLARE_TEX2DARRAY(_AnimationTex);
            //GPUInstancing属性
            UNITY_INSTANCING_BUFFER_START(Props)
                    UNITY_DEFINE_INSTANCED_PROP(float, _AnimationSize)
                    UNITY_DEFINE_INSTANCED_PROP(float4, _Scale)
                    UNITY_DEFINE_INSTANCED_PROP(uint, _FPS)
                    UNITY_DEFINE_INSTANCED_PROP(uint, _VertexNum)
                    UNITY_DEFINE_INSTANCED_PROP(float4, _TextureSize)
            UNITY_INSTANCING_BUFFER_END(Props)

            float4 SampleAnimationTex(appdata v)
            {
                float animaTime = UNITY_ACCESS_INSTANCED_PROP(Props, _AnimationSize);
                float passedTime = _Time.y;
                float time = passedTime - floor(passedTime / animaTime) * animaTime;
                uint nowFrame = floor(time * (UNITY_ACCESS_INSTANCED_PROP(Props, _FPS) - 1));
                uint vertexNum = UNITY_ACCESS_INSTANCED_PROP(Props, _VertexNum);
                float4 textureSize = UNITY_ACCESS_INSTANCED_PROP(Props, _TextureSize);
                uint framePerTex = floor(textureSize.x * textureSize.y  * 1.0 / (2 * vertexNum));
                //获取当前是第几帧和使用第几个贴图
                uint currentFrame = nowFrame % framePerTex;
                uint currentTextureIndex = floor(nowFrame / framePerTex);
                uint nowVertex = currentFrame * vertexNum * 2 + v.vertexId * 2;
                uint row = floor(nowVertex / textureSize.y);
                //获取采样UV
                float uvX = row * 1.0f / textureSize.x;
                float uvY = (nowVertex % textureSize.y) * 1.0 / textureSize.y;
                float4 sampleResult = UNITY_SAMPLE_TEX2DARRAY_LOD(_AnimationTex, float3(uvX, uvY, currentTextureIndex), 0);
                float4 animScale = UNITY_ACCESS_INSTANCED_PROP(Props, _Scale);
                float4 finalResult = float4(
                                        (sampleResult.x * 2 - 1) * animScale.x,
                                        (sampleResult.y * 2 - 1) * animScale.y,
                                        (sampleResult.z * 2 - 1) * animScale.z,
                                        0);
                return finalResult;
            }

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                o.color = SampleAnimationTex(v);
                o.vertex = UnityObjectToClipPos(o.color);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                fixed4 col = tex2D(_MainTex, i.uv);
                return col;
            }
            ENDCG
        }
    }
}

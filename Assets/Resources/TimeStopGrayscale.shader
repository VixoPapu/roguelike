Shader "ProjectLike/TimeStopGrayscale"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _TimeStopTexture ("Preserved Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
        [PerRendererData] _AlphaTex ("External Alpha", 2D) = "white" {}
        [PerRendererData] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "CanUseSpriteAtlas"="True" }
        Cull Off Lighting Off ZWrite Off Blend One OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex SpriteVert
            #pragma fragment GrayFragment
            #pragma multi_compile_instancing
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
            #include "UnitySprites.cginc"
            sampler2D _TimeStopTexture;
            fixed4 GrayFragment(v2f IN):SV_Target
            {
                fixed4 c=tex2D(_TimeStopTexture, IN.texcoord)*IN.color;
                fixed gray=dot(c.rgb,fixed3(.299,.587,.114));
                return fixed4(gray*c.a,gray*c.a,gray*c.a,c.a);
            }
            ENDCG
        }
    }
}

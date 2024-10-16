Shader "Custom/VertexColorShader"
{
    Properties
    {
        // 他のプロパティが必要な場合はここに追加
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 color : COLOR; // 頂点カラー
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 color : COLOR; // 頂点カラーをフラグメントに渡す
                float3 normal : NORMAL;
            };

            v2f vert(appdata_t v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color; // 頂点カラーを設定
                o.normal = UnityObjectToWorldNormal(v.normal);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // 環境光
                fixed3 ambient = 0.1 * i.color.rgb;

                // 法線に基づくフォンライティング
                fixed3 lightDir = normalize(_WorldSpaceLightPos0.xyz); // ライト方向
                float diff = max(0, dot(i.normal, lightDir)); // ディフューズ
                fixed3 diffuse = diff * i.color.rgb;

                return fixed4(ambient + diffuse, 1.0);
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
}
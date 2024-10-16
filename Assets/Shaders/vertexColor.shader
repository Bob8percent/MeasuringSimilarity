Shader "Custom/VertexColorShader"
{
    Properties
    {
        // ���̃v���p�e�B���K�v�ȏꍇ�͂����ɒǉ�
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
                float4 color : COLOR; // ���_�J���[
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 color : COLOR; // ���_�J���[���t���O�����g�ɓn��
                float3 normal : NORMAL;
            };

            v2f vert(appdata_t v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color; // ���_�J���[��ݒ�
                o.normal = UnityObjectToWorldNormal(v.normal);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // ����
                fixed3 ambient = 0.1 * i.color.rgb;

                // �@���Ɋ�Â��t�H�����C�e�B���O
                fixed3 lightDir = normalize(_WorldSpaceLightPos0.xyz); // ���C�g����
                float diff = max(0, dot(i.normal, lightDir)); // �f�B�t���[�Y
                fixed3 diffuse = diff * i.color.rgb;

                return fixed4(ambient + diffuse, 1.0);
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
}
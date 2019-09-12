#include "UnityCG.cginc"

struct appdata
{
	float4 vertex : POSITION;
	float3 normal : NORMAL;
};

struct v2f
{
	float4 vertex : SV_POSITION;
	float3 normal : NORMAL;
};

float4 _Color;

v2f vert(appdata v)
{
	v2f o;
	o.vertex = UnityObjectToClipPos(v.vertex);
	o.normal = UnityObjectToWorldDir(v.normal);
	return o;
}

fixed4 frag(v2f i) : SV_Target
{
	return _Color * dot(_WorldSpaceLightPos0.xyz, i.normal) * 0.5f + _Color * 0.5f;
}
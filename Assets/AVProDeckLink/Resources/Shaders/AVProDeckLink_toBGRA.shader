//-----------------------------------------------------------------------------
// Copyright 2014-2018 RenderHeads Ltd.  All rights reserved.
//-----------------------------------------------------------------------------

Shader "AVProDeckLink/RGBA 4:4:4 to BGRBA 4:4:4"
{
	Properties 
	{
		_MainTex ("Base (RGB)", 2D) = "white" {}
	}
	SubShader 
	{
		Pass
		{ 
			ZTest Always Cull Off ZWrite Off
			Fog { Mode off }
		
CGPROGRAM
#pragma vertex vert
#pragma fragment frag
#pragma exclude_renderers flash xbox360 ps3 gles
//#pragma fragmentoption ARB_precision_hint_fastest
#pragma fragmentoption ARB_precision_hint_nicest
//#pragma only_renderers d3d11 
//#pragma fragmentoption ARB_precision_hint_fastest 
#pragma multi_compile SWAP_RED_BLUE_ON SWAP_RED_BLUE_OFF
#pragma multi_compile CHROMA_NOLERP CHROMA_LERP CHROMA_SMARTLERP
#pragma multi_compile __ APPLY_GAMMA
#pragma multi_compile __ IGNORE_ALPHA
#include "UnityCG.cginc"
#include "AVProDeckLink_Shared.cginc"

uniform sampler2D _MainTex;
uniform float4 _MainTex_ST;
uniform float4 _MainTex_TexelSize;

struct v2f {
	float4 pos : POSITION;
	float2 uv : TEXCOORD0;
};

v2f vert( appdata_img v )
{
	v2f o;
	o.pos = UnityObjectToClipPos (v.vertex);
	o.uv = float4(TRANSFORM_TEX(v.texcoord, _MainTex), 0, 0);
	
	o.uv.y = 1-o.uv.y;
	
	// On D3D when AA is used, the main texture & scene depth texture
	// will come out in different vertical orientations.
	// So flip sampling of the texture when that is the case (main texture
	// texel size will have negative Y).
	#if SHADER_API_D3D9
	if (_MainTex_TexelSize.y < 0)
	{
		o.uv.y = 1-o.uv.y;
	}
	#endif
	
	return o;
}

float4 frag (v2f i) : COLOR
{
	fixed4 col = tex2D(_MainTex, i.uv.xy);

#if APPLY_GAMMA
	col.rgb = linearToGamma(col.rgb);
#endif

#if IGNORE_ALPHA
	col.a = 1.0;
#endif

	return col.bgra;
} 
ENDCG
		}
	}
	
	FallBack Off
}
//-----------------------------------------------------------------------------
// Copyright 2014-2018 RenderHeads Ltd.  All rights reserved.
//-----------------------------------------------------------------------------

Shader "AVProDeckLink/RGBA 4:4:4 to UYVY 4:2:2"
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
#include "UnityCG.cginc"
#include "AVProDeckLink_Shared.cginc"

uniform sampler2D _MainTex;
uniform float4 _MainTex_ST;
uniform float4 _MainTex_TexelSize;

struct v2f {
	float4 pos : POSITION;
	float4 uv : TEXCOORD0;
};

v2f vert( appdata_img v )
{
	v2f o;
	o.pos = UnityObjectToClipPos (v.vertex);
	o.uv = float4(TRANSFORM_TEX(v.texcoord, _MainTex), 0.0, 0.0);
	
	o.uv.y = 1.0 - o.uv.y;
	
	// On D3D when AA is used, the main texture & scene depth texture
	// will come out in different vertical orientations.
	// So flip sampling of the texture when that is the case (main texture
	// texel size will have negative Y).
	#if SHADER_API_D3D9
	if (_MainTex_TexelSize.y < 0)
	{
		o.uv.y = 1.0 - o.uv.y;
	}
	#endif
	
	//o.uv.z = v.vertex.x * _TextureWidth * 0.5;

	return o;
}

float4 frag (v2f i) : COLOR
{
	float4 uv = i.uv;
	
	float4 col1 = tex2D(_MainTex, uv.xy);
	float4 col2 = tex2D(_MainTex, uv.xy + float2(_MainTex_TexelSize.x, 0.0));

#if APPLY_GAMMA
	col1.rgb = linearToGamma(col1.rgb);
	col2.rgb = linearToGamma(col2.rgb);
#endif

	float3 yuv1 = ConvertRGB_YUV_HD(col1.r, col1.g, col1.b);
	float3 yuv2 = ConvertRGB_YUV_HD(col2.r, col2.g, col2.b);
	
	float u = (yuv1.y + yuv2.y) / 2.0;
	float v = (yuv1.z + yuv2.z) / 2.0;
	float y1 = yuv1.x;
	float y2 = yuv2.x;
	float4 oCol = float4(u, y1, v, y2);

	return oCol;
} 
ENDCG
		}
	}
	
	FallBack Off
}
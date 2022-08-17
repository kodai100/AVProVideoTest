//-----------------------------------------------------------------------------
// Copyright 2014-2018 RenderHeads Ltd.  All rights reserved.
//-----------------------------------------------------------------------------

Shader "AVProDeckLink/CompositeARGB"
{
	Properties 
	{
		_MainTex ("Base (RGB)", 2D) = "white" {}
		_TextureScaleOffset ("Scale OFfset", Vector) = (1, 1, 0, 0)
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
#pragma multi_compile __ APPLY_LINEAR
#pragma multi_compile __ IGNORE_ALPHA

#include "UnityCG.cginc"
#include "AVProDeckLink_Shared.cginc"

uniform sampler2D _MainTex;
uniform float4 _TextureScaleOffset;
uniform float4 _MainTex_TexelSize;

struct v2f {
	float4 pos : POSITION;
	float4 uv : TEXCOORD0;
};

v2f vert( appdata_img v )
{
	v2f o;
	o.uv = float4(0, 0, 0, 0);
	o.pos = UnityObjectToClipPos (v.vertex);
	
	o.uv.xy = (v.texcoord.xy * _TextureScaleOffset.xy + _TextureScaleOffset.zw);
	
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
	float4 oCol = tex2D(_MainTex, i.uv.xy).abgr;

#if defined(SWAP_RED_BLUE_ON)
	oCol = oCol.bgra;
#endif

#if APPLY_LINEAR
	oCol.rgb = gammaToLinear(oCol.rgb);
#endif

#if IGNORE_ALPHA
	oCol.a = 1.0;
#endif

	return float4(oCol.rgb, oCol.a);
} 

ENDCG
		}
	}
	
	FallBack Off
}
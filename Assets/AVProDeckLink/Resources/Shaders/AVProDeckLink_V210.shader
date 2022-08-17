//-----------------------------------------------------------------------------
// Copyright 2014-2018 RenderHeads Ltd.  All rights reserved.
//-----------------------------------------------------------------------------

Shader "AVProDeckLink/CompositeV210"
{
	Properties 
	{
		_MainTex ("Base (RGB)", 2D) = "white" {}
		_TargetTextureWidth ("Target Texure Width", Float) = 256.0
	}
	SubShader 
	{
		Pass
		{ 
			ZTest Always 
			Cull Off 
			ZWrite Off
			Fog { Mode off }

CGPROGRAM
#pragma vertex vert
#pragma fragment frag
#pragma target 4.0
#pragma only_renderers d3d11 
#pragma multi_compile __ APPLY_LINEAR
#pragma multi_compile USE_REC709 USE_REC2020 USE_REC2100
//#pragma exclude_renderers flash
//#pragma fragmentoption ARB_precision_hint_fastest 
#pragma fragmentoption ARB_precision_hint_nicest
//#pragma multi_compile SWAP_RED_BLUE_ON SWAP_RED_BLUE_OFF
#include "UnityCG.cginc"
#include "AVProDeckLink_Shared.cginc"

uniform sampler2D _MainTex;
uniform float _TargetTextureWidth;
uniform float4 _MainTex_TexelSize;
uniform float4 _TextureScaleOffset;

struct v2f {
  float4 pos : POSITION;
  float4 uv : TEXCOORD0;
};

v2f vert( appdata_img v )
{
  v2f o;
  o.pos = UnityObjectToClipPos (v.vertex);
  o.uv = float4(0.0, 0.0, 0.0, 0.0);

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

  o.uv.z = v.texcoord.x * _TargetTextureWidth;
  
  return o;
}

float3 Convert(float3 yCbCr)
{
#if USE_REC709

	float3 col = convertYUV_HD(yCbCr.x, yCbCr.z, yCbCr.y);
#if APPLY_LINEAR
	col = gammaToLinear(col);
#endif

#elif USE_REC2020

	float3 col = YUVDecode2020(yCbCr);
	col = Encode2020to709(col);
	col = convertYUV_HD(yCbCr.x, yCbCr.z, yCbCr.y);
#if APPLY_LINEAR
	col = gammaToLinear(col);
#endif

#elif USE_REC2100

	float3 col = HLG_ConvertYUV_RGB(yCbCr);
	col = HLG_Decode(col);

#else

	float3 col = 0.0;

#endif

	return col;
}

float4 frag (v2f i) : COLOR
{
	float4 oCol = 0.0;
	
	float4 uv = i.uv;

	int x = floor(fmod(uv.z, 6.0));
	
	uint range = 255;
	
	if (x == 0)
	{	
		float3 r1 = tex2Dlod(_MainTex, float4(uv.xy, 0.0, 0.0)).bgr;
		oCol.rgb = Convert(float3(r1.g, r1.b, r1.r));
	}
	else if (x == 1)
	{
		float3 r1 = tex2Dlod(_MainTex, float4(uv.xy - float2(_MainTex_TexelSize.x*0.0, 0.0), 0.0, 0.0)).bgr;
		float3 r2 = tex2Dlod(_MainTex, float4(uv.xy + float2(_MainTex_TexelSize.x*1.0, 0.0), 0.0, 0.0)).bgr;
		oCol.rgb = Convert(float3(r2.b, r1.b, r1.r));
	}
	else if (x == 2)
	{
		float3 r1 = tex2Dlod(_MainTex, float4(uv.xy - float2(_MainTex_TexelSize.x*0.0, 0.0), 0.0, 0.0)).bgr;
		float3 r2 = tex2Dlod(_MainTex, float4(uv.xy + float2(_MainTex_TexelSize.x*1.0, 0.0), 0.0, 0.0)).bgr;
		oCol.rgb = Convert(float3(r1.r, r1.g, r2.b));
	}
	else if (x == 3)
	{
		uv.x -= _MainTex_TexelSize.x * 1;
		float3 r1 = (tex2D(_MainTex, uv.xy).bgr);
		float3 r2 = tex2D(_MainTex, uv.xy + float2(_MainTex_TexelSize.x, 0.0)).bgr;
		oCol.rgb = Convert(float3(r2.g, r1.g, r2.b));
	}
	else if (x == 4)
	{
		float3 r1 = tex2Dlod(_MainTex, float4(uv.xy - float2(_MainTex_TexelSize.x*0.0, 0.0), 0.0, 0.0)).bgr;
		float3 r2 = tex2Dlod(_MainTex, float4(uv.xy + float2(_MainTex_TexelSize.x*1.0, 0.0), 0.0, 0.0)).bgr;
		oCol.rgb = Convert(float3(r2.b, r1.r, r2.g));
	}
	else if (x == 5)
	{
		float3 r1 = tex2Dlod(_MainTex, float4(uv.xy - float2(_MainTex_TexelSize.x*1.0, 0.0), 0.0, 0.0)).bgr;
		float3 r2 = tex2Dlod(_MainTex, float4(uv.xy + float2(_MainTex_TexelSize.x*0.0, 0.0), 0.0, 0.0)).bgr;
		oCol.rgb = Convert(float3(r2.r, r1.r, r2.g));
	}

	oCol.a = 1.0;	

	return oCol;
} 
ENDCG


		}
	}
	
	FallBack Off
}
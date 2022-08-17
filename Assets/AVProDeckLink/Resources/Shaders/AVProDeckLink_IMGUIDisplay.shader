﻿//-----------------------------------------------------------------------------
// Copyright 2014-2018 RenderHeads Ltd.  All rights reserved.
//-----------------------------------------------------------------------------

Shader "AVProDeckLink/IMGUIDisplay"
{
	Properties
	{
		_MainTex ("Texture", 2D) = "white" {}
	}
	SubShader
	{
		Tags { "RenderType"="Opaque" }
		LOD 100
		Blend SrcAlpha OneMinusSrcAlpha

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag 
			#pragma multi_compile __ APPLY_GAMMA
			#pragma multi_compile SCALE_TO_FIT SCALE_AND_CROP STRETCH_TO_FILL

			#include "UnityCG.cginc"
			#include "AVProDeckLink_Shared.cginc"

			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
			};

			struct v2f
			{
				float2 uv : TEXCOORD0;
				float4 vertex : SV_POSITION;
			};

			uniform sampler2D _MainTex;
			uniform float4 _MainTex_ST;
			uniform float _width;
			uniform float _height;
			uniform float _rectWidth;
			uniform float _rectHeight;
			uniform float4 _color;
			uniform sampler2D _RightEyeTex;
			uniform  int _EyeMode;
			
			v2f vert (appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = TRANSFORM_TEX(v.uv, _MainTex);

				float rectAspect = _rectWidth / _rectHeight;
				float texAspect = _width / _height;
#if SCALE_TO_FIT
				float2 multiplier = rectAspect <= texAspect ? float2(1, texAspect / rectAspect) : float2(rectAspect / texAspect, 1);
				float2 newuv = o.uv * multiplier;
				float2 dif = float2(1, 1) - multiplier;
				o.uv = newuv + dif / 2;
#elif SCALE_AND_CROP
				float2 multiplier = rectAspect <= texAspect ? float2(rectAspect / texAspect, 1) : float2(1, texAspect / rectAspect);
				float2 newuv = o.uv * multiplier;
				float2 dif = multiplier - float2(1, 1);
				o.uv = newuv - dif / 2;
#elif STRETCH_TO_FILL
				//nothing needs to be done for stretch
#endif

				return o;
			}
			
			fixed4 frag (v2f i) : SV_Target
			{
				bool inBounds = i.uv.x >= 0 && i.uv.y >= 0 && i.uv.x <= 1 && i.uv.y <= 1;

				bool useLeft = true;
				if (_EyeMode < 0)
				{
					useLeft = true;
				}
				else if (_EyeMode > 0)
				{
					useLeft = false;
				}
				else
				{
					useLeft = IsStereoEyeLeft(_WorldSpaceCameraPos, UNITY_MATRIX_V[0].xyz);
				}

				fixed4 col;
				
				if (useLeft)
				{
					col = inBounds ? tex2D(_MainTex, i.uv) : fixed4(0, 0, 0, 0);
				}
				else
				{
					col = inBounds ? tex2D(_RightEyeTex, i.uv) : fixed4(0, 0, 0, 0);
				}

				col *= _color;
#if APPLY_GAMMA
				col.rgb = linearToGamma(col.rgb);
#endif
				//return fixed4(i.uv, 0, 1);
				return col;
			}
			ENDCG
		}
	}
}

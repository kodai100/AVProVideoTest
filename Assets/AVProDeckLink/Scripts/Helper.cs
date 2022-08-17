﻿using UnityEngine;

//-----------------------------------------------------------------------------
// Copyright 2014-2022 RenderHeads Ltd.  All rights reserved.
//-----------------------------------------------------------------------------

namespace RenderHeads.Media.AVProDeckLink
{
	public enum Eye
	{
		Left = -1,
		Both = 0,
		Right = 1,
	}

	public enum InterlacePass
	{
		First = 0,
		Second = 1,
	}

	public class Helper
	{
		public static string Version = "1.9.3";

		public static bool HasAlphaChannel(RenderTextureFormat format)
		{
			bool result = false;
			switch (format)
			{
				case RenderTextureFormat.ARGB32:
				#if UNITY_5_4_OR_NEWER
				case RenderTextureFormat.BGRA32:
				#endif
				case RenderTextureFormat.ARGB4444:
				case RenderTextureFormat.ARGB1555:
				case RenderTextureFormat.ARGB2101010:
				#if UNITY_5_6_OR_NEWER
				case RenderTextureFormat.ARGB64:
				case RenderTextureFormat.RGBAUShort:
				#endif
				case RenderTextureFormat.ARGBInt:
				case RenderTextureFormat.ARGBFloat:
				case RenderTextureFormat.ARGBHalf:
				#if UNITY_2017_2_OR_NEWER
				case RenderTextureFormat.BGRA10101010_XR:
				#endif
					result = true;
					break;
			}
			return result;
		}

		public static void SavePNG(string filePath, RenderTexture rt)
		{
			if (rt != null)
			{
				RenderTexture resolve = RenderTexture.GetTemporary(rt.width, rt.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
				Graphics.Blit(rt, resolve);
				Texture2D tex = new Texture2D(rt.width, rt.height, TextureFormat.ARGB32, false, true);
				RenderTexture.active = resolve;
				tex.ReadPixels(new Rect(0, 0, tex.width, tex.height), 0, 0, false);
				tex.Apply(false, false);
				RenderTexture.ReleaseTemporary(resolve);

#if !UNITY_WEBPLAYER
				byte[] pngBytes = tex.EncodeToPNG();
				System.IO.File.WriteAllBytes(filePath, pngBytes);
#endif
				RenderTexture.active = null;
				Texture2D.Destroy(tex);
				tex = null;
			}
		}

		// EXR exporting is only supported in Unity 5.6 and above
#if UNITY_5_6_OR_NEWER
		public static void SaveEXR(string filePath, RenderTexture rt)
		{
			if (rt != null)
			{
				if (rt.format != RenderTextureFormat.ARGBFloat &&
					rt.format != RenderTextureFormat.ARGBHalf &&
					rt.format != RenderTextureFormat.ARGB2101010)
				{
					Debug.LogError("Writing to EXR requires floating point texture " + rt.format);
					return;
				}

				Texture2D tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBAFloat, false);
				RenderTexture.active = rt;
				tex.ReadPixels(new Rect(0, 0, tex.width, tex.height), 0, 0, false);
				tex.Apply(false, false);

#if !UNITY_WEBPLAYER
				byte[] exrBytes = tex.EncodeToEXR(Texture2D.EXRFlags.OutputAsFloat);
				System.IO.File.WriteAllBytes(filePath, exrBytes);
#endif
				RenderTexture.active = null;
				Texture2D.Destroy(tex);
				tex = null;
			}
		}
#endif
	}
}

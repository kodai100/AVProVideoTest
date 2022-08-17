#if UNITY_4_1 || UNITY_4_2 || UNITY_4_3 || UNITY_4_4 || UNITY_4_5 || UNITY_4_6 || UNITY_4_7 || UNITY_5 || UNITY_5_4_OR_NEWER
#define AVPRODECKLINK_UNITYFEATURE_NONPOW2TEXTURES
#endif
//#define LIVE_SHADER_UPDATE

using UnityEngine;
using System.Text;
using System.Collections.Generic;
using System.Runtime.InteropServices;

//-----------------------------------------------------------------------------
// Copyright 2014-2020 RenderHeads Ltd.  All rights reserved.
//-----------------------------------------------------------------------------

namespace RenderHeads.Media.AVProDeckLink
{
	[System.Serializable]
	public class FormatConverter : System.IDisposable
	{
		private enum ConversionMethod
		{
			None,
			PixelShader,
			ComputeShader,
		}

		private struct Eye
		{
			public Texture2D rawTexture;
			public RenderTexture convertedTexture;
			public RenderTexture deinterlacedTexture;
		}

		private int _deviceHandle;

		private Eye[] _eye = new Eye[2];

		// Format conversion and texture output
		private ConversionMethod _conversionMethod = ConversionMethod.None;
		private int _usedTextureWidth, _usedTextureHeight;

		// Format conversion by pixel shader
		private Material _conversionMaterial;
		private int _conversionMaterialPass;

		// Format conversion by compute shader
		private ComputeShader _computeShader;
		private int	_computeKernel;
		private ComputeShader _computeV210ToRGB;
		private ComputeShader _computeR210ToRGB;
		private ComputeBuffer _computeParameters;

		// Deinterlace
		private Material _deinterlaceMaterial;

		//for 3D
		private bool _enable3D;

		// Conversion params
		private DeviceMode _mode;
		private bool _autoDeinterlace;
		private bool _deinterlace;
		private bool _useHdr;
		private int _lastFrameUploaded = -1;
		private bool _isBuilt;

		private bool _flipX = false;
		private bool _flipY = false;
		private bool _ignoreAlphaChannel = true;
		private bool _bypassGammaCorrection = false;
		private bool _isConversionMaterialDirty = false;

		private DeckLinkManager.DeinterlaceMethod _deinterlaceMethod = DeckLinkManager.DeinterlaceMethod.Blend;

		private const string ShaderKeyRec709 = "USE_REC709";
		private const string ShaderKeyRec2100 = "USE_REC2100";
		private const string ShaderKeyRec2020 = "USE_REC2020";
		private const string ShaderKeyIgnoreAlpha = "IGNORE_ALPHA";
		private const string ShaderApplyLinear = "APPLY_LINEAR";

		private int _propTargetTextureWidth;
		private int _propTextureScaleOffset;
		private int _propRawTexture;
		private int _propResult;
		private int _propConstBuffer;

		private DeckLink.ColorspaceMode _colorspaceMode = DeckLink.ColorspaceMode.Rec709;
		private System.IntPtr _externalTexturePtr = System.IntPtr.Zero;

		public Texture OutputTexture
		{
			get { return _deinterlace?_eye[0].deinterlacedTexture:_eye[0].convertedTexture; }
		}

		public Texture RightEyeOutputTexture
		{
			get { return _deinterlace?_eye[1].deinterlacedTexture:_eye[1].convertedTexture; }
		}

		public DeckLink.ColorspaceMode ColorspaceMode
		{
			set
			{
				if (_colorspaceMode != value) { _colorspaceMode = value; _isConversionMaterialDirty = true; }
			}
			get
			{
				return _colorspaceMode;
			}
		}

		public bool Enable3DInput
		{
			get
			{
				return _enable3D;
			}
			set
			{
				_enable3D = value;
			}
		}

		public bool IgnoreAlphaChannel
		{
			get { return _ignoreAlphaChannel; }
			set { if (_ignoreAlphaChannel != value) { _ignoreAlphaChannel = value; _isConversionMaterialDirty = true; } }
		}

		public bool BypassGammaCorrection
		{
			get { return _bypassGammaCorrection; }
			set { if (_bypassGammaCorrection != value) { _bypassGammaCorrection = value; _isConversionMaterialDirty = true; } }
		}

		public bool FlipX
		{
			get { return _flipX; }
			set { if (_flipX != value) { _flipX = value; _isConversionMaterialDirty = true; } }
		}

		public bool FlipY
		{
			get { return _flipY; }
			set { if (_flipY != value) { _flipY = value; _isConversionMaterialDirty = true; } }
		}

		public int OutputFrameNumber
		{
			get { return _lastFrameUploaded; }
		}

		public bool ValidPicture { get; private set; }
		public bool AutoDeinterlace
		{
			get { return _autoDeinterlace; }
			set { _autoDeinterlace = value; if (_mode != null) _deinterlace = (AutoDeinterlace && _mode.InterlacedFieldMode); }
		}

		public void Reset()
		{
			ValidPicture = false;
			_lastFrameUploaded = -1;
			_propTargetTextureWidth = Shader.PropertyToID("_TargetTextureWidth");
			_propTextureScaleOffset = Shader.PropertyToID("_TextureScaleOffset");
			_propRawTexture = Shader.PropertyToID("rawTexture");
			_propResult = Shader.PropertyToID("result");
			_propConstBuffer = Shader.PropertyToID("constBuffer");
		}

		public bool Build(int deviceHandle, DeviceMode mode, bool delayResourceCreationUntilFramesStart = false, bool useHdr = false)
		{
			bool result = true;
			Reset();

			_deviceHandle = deviceHandle;
			_mode = mode;
			_deinterlace = (AutoDeinterlace && _mode.InterlacedFieldMode);
			_useHdr = useHdr;

			_isBuilt = false;
			if (!delayResourceCreationUntilFramesStart)
			{
				result = Build();
			}
			return result;
		}

		private bool Build()
		{
			if (CreateMaterials())
			{
				CreateRawTexture();

				if (_eye[0].rawTexture)
				{
					CreateConvertedTexture();
					UpdateConversionMaterial();
				}
				else
				{
					Debug.LogWarning("[AVProDeckLink] FormatConverter failed to create raw texture");
				}
			}
			else
			{
				Debug.LogWarning("[AVProDeckLink] FormatConverter failed to create materials");
			}

			_isBuilt = ((_computeShader != null || _conversionMaterial != null) && _eye[0].rawTexture != null && _eye[0].convertedTexture != null);
			return _isBuilt;
		}

		public bool Update()
		{
			bool result = false;

			bool build = !_isBuilt;

			build |= (_externalTexturePtr != DeckLinkPlugin.GetTexturePointer(_deviceHandle));

			if (build)
			{
				if (DeckLinkPlugin.GetLastCapturedFrameTime(_deviceHandle) > 0)
				{
					if (!Build())
						return false;
				}
				return false;
			}

			// Wait until next frame has been uploaded to the texture
			int lastFrameUploaded = (int)DeckLinkPlugin.GetLastFrameUploaded(_deviceHandle);
			if (_lastFrameUploaded != lastFrameUploaded)
			{
				RenderTexture prev = RenderTexture.active;

				// Format convert
				for (int i = 0; i < 2; i++)
				{
					if (i == 0 || _enable3D)
					{
						if (_eye[i].convertedTexture && _eye[i].rawTexture)
						{
							result = DoFormatConversion(_eye[i].convertedTexture, _eye[i].rawTexture);
							if (!result)
							{
								break;
							}

							// Deinterlace
							if (_deinterlace)
							{
								RecreateConvertedTexture(_eye[i].convertedTexture.format, ref _eye[i].deinterlacedTexture, true, _eye[i].convertedTexture.enableRandomWrite);
								if (_eye[i].deinterlacedTexture)
								{ 
									_eye[i].convertedTexture.filterMode = FilterMode.Point;
									DoDeinterlace(_eye[i].convertedTexture, _eye[i].deinterlacedTexture);
								}
							}
						}
					}
				}

				RenderTexture.active = prev;

				_lastFrameUploaded = lastFrameUploaded;
			}
			else if (	(_eye[0].convertedTexture && !_eye[0].convertedTexture.IsCreated()) || 
						(_eye[1].convertedTexture && !_eye[1].convertedTexture.IsCreated())	)
			{
				Debug.LogError("GPU Reset");
				// If the texture has been lost due to GPU reset(from full screen mode change or vsync change) we'll need fill the texture again
				Reset();
			}

			return ValidPicture && result;
		}

		public void Dispose()
		{
			_mode = null;
			ValidPicture = false;

			if (_computeParameters != null)
			{
				_computeParameters.Release();
				_computeParameters = null;
			}
			_computeKernel = -1;
			_computeShader = null;
			_computeV210ToRGB = null;
			_computeR210ToRGB = null;

			if (_conversionMaterial != null)
			{
				_conversionMaterial.mainTexture = null;
				Material.Destroy(_conversionMaterial);
				_conversionMaterial = null;
			}

			if (_deinterlaceMaterial != null)
			{
				_deinterlaceMaterial.mainTexture = null;
				Material.Destroy(_deinterlaceMaterial);
				_deinterlaceMaterial = null;
			}

			for (int i = 0; i < 2; i++)
			{
				if (_eye[i].deinterlacedTexture != null)
				{
					RenderTexture.ReleaseTemporary(_eye[i].deinterlacedTexture);
					_eye[i].deinterlacedTexture = null;
				}

				if (_eye[i].convertedTexture != null)
				{
					RenderTexture.ReleaseTemporary(_eye[i].convertedTexture);
					_eye[i].convertedTexture = null;
				}

				_eye[i].rawTexture = null;
			}
		}

		private bool CreateMaterials()
		{
			_conversionMethod = ConversionMethod.None;
			Shader shader = null;
			int pass = 0;
			if (DeckLinkManager.Instance.GetPixelConversionShader(_mode.PixelFormat, ref shader, ref pass))
			{
				if (_conversionMaterial != null)
				{
					if (_conversionMaterial.shader != shader)
					{
						Material.Destroy(_conversionMaterial);
						_conversionMaterial = null;
					}
				}

				if (_conversionMaterial == null)
				{
					_conversionMaterial = new Material(shader);
					_conversionMaterial.name = "AVProDeckLink-Material";
				}

				_conversionMaterialPass = pass;
				_conversionMethod = ConversionMethod.PixelShader;
			}
			else
			{
				if (_mode.PixelFormat == DeckLinkPlugin.PixelFormat.YCbCr_10bpp_422)
				{
					if (_computeV210ToRGB == null)
					{
						_computeV210ToRGB = (ComputeShader)Resources.Load("Shaders/AVProDeckLink_V210_to_RGBA");
					}
					_computeShader = _computeV210ToRGB;
					_conversionMethod = ConversionMethod.ComputeShader;
				}
				else if (_mode.PixelFormat == DeckLinkPlugin.PixelFormat.RGB_10bpp_444)
				{
					if (_computeR210ToRGB == null)
					{
						_computeR210ToRGB = (ComputeShader)Resources.Load("Shaders/AVProDeckLink_R210_to_RGBA");
					}
					_computeShader = _computeR210ToRGB;
					_conversionMethod = ConversionMethod.ComputeShader;
				}
			}

			// Deinterlace
			if (true)
			{
				shader = DeckLinkManager.Instance.GetDeinterlaceShader();
				if (shader)
				{
					if (_deinterlaceMaterial != null)
					{
						if (_deinterlaceMaterial.shader != shader)
						{
							Material.Destroy(_deinterlaceMaterial);
							_deinterlaceMaterial = null;
						}
					}

					if (_deinterlaceMaterial == null)
					{
						_deinterlaceMaterial = new Material(shader);
						_deinterlaceMaterial.name = "AVProDeckLink-DeinterlaceMaterial";
						UpdateDeinterlaceMaterial(DeckLinkManager.Instance._deinterlaceMethod);
					}
				}
			}

			return (_computeShader != null || _conversionMaterial != null) && (_deinterlaceMaterial != null);
		}

		private void CreateRawTexture()
		{
			System.IntPtr texPtr = DeckLinkPlugin.GetTexturePointer(_deviceHandle);
			if (texPtr != System.IntPtr.Zero)
			{
				_usedTextureHeight = _mode.Height;

				// NOTE: some textures have padding, eg YUV422 10-bit must be a multiple of 48
				// so 4K DCI requires padding

				if (_mode.PixelFormat == DeckLinkPlugin.PixelFormat.YCbCr_10bpp_422)
				{
					_usedTextureWidth = ((_mode.Width * 16) / 6) / 4;
				}
				else
				{
					_usedTextureWidth = _mode.Pitch / 4;
				}

				int textureWidth = _usedTextureWidth;
				int textureHeight = _usedTextureHeight;

				// Account for potential padding
				TextureFormat textureFormat = TextureFormat.ARGB32;
				if (_mode.PixelFormat == DeckLinkPlugin.PixelFormat.YCbCr_10bpp_422)
				{
					textureWidth = ((_mode.Width + 47) / 48) * 32;
#if UNITY_2022_1_OR_NEWER
					// RJT NOTE: It seems the format parameter is mostly ignored, except under Unity 2022(+?)/D3D12, where it appears to create
					// a 'default' SRV for it, but unfortunately there isn't an obvious 'TextureFormat' for our native 'DXGI_FORMAT_R10G10B10A2_UNORM'
					// format and it crashes, so for now forcing it to a format that appears to bypass invalid SRV creation..
					textureFormat = TextureFormat.ETC_RGB4;//DXGI_FORMAT_R10G10B10A2_UNORM;
#endif
				}
				else if (_mode.PixelFormat == DeckLinkPlugin.PixelFormat.RGB_10bpp_444)
				{
					textureWidth = ((_mode.Width + 63) / 64) * 64;
#if UNITY_2022_1_OR_NEWER
					textureFormat = TextureFormat.ETC_RGB4;//DXGI_FORMAT_R8G8B8A8_UINT;
#endif
				}

				for (int i = 0; i < 2; i++)
				{
					if (i == 1)
					{
						texPtr = DeckLinkPlugin.GetRightTexturePointer(_deviceHandle);
					}
					if (texPtr != System.IntPtr.Zero)
					{
						_eye[i].rawTexture = Texture2D.CreateExternalTexture(textureWidth, textureHeight, textureFormat, false, true, texPtr);
						_eye[i].rawTexture.wrapMode = TextureWrapMode.Clamp;
						_eye[i].rawTexture.filterMode = FilterMode.Point;

						if (i == 0)
						{
							_eye[i].rawTexture.name = "AVProDeckLink-RawTexture";
							_externalTexturePtr = texPtr;
						}
						else
						{
							_eye[i].rawTexture.name = "AVProDeckLink-RawTexture-Right";
						}
					}
				}
			}
		}

		private void CreateConvertedTexture()
		{
			// For 10-bit formats we render to higher precision buffers
			// TODO: make this optional?
			RenderTextureFormat format = RenderTextureFormat.ARGB32;
			if (_useHdr || IsHighBitDepth(_mode.PixelFormat))
			{
				format = RenderTextureFormat.ARGBHalf;
			}

			bool enableRandomWrite = (_conversionMethod == ConversionMethod.ComputeShader);

			// Create RenderTexture for post transformed frames
			RecreateConvertedTexture(format, ref _eye[0].convertedTexture, true, enableRandomWrite);
			RecreateConvertedTexture(format, ref _eye[1].convertedTexture, _enable3D, enableRandomWrite);
		}

		private void RecreateConvertedTexture(RenderTextureFormat format, ref RenderTexture texture, bool isUsed, bool enableRandomWrite)
		{
			// If there is already a renderTexture, destroy it doesn't match the desired size
			if (texture != null)
			{
				if (!isUsed || 
					texture.width != _mode.Width ||
					texture.height != _mode.Height ||
					texture.format != format ||
					texture.enableRandomWrite != enableRandomWrite)
				{
					RenderTexture.ReleaseTemporary(texture);
					texture = null;
				}
			}

			if (texture == null && isUsed)
			{
				texture = RenderTexture.GetTemporary(_mode.Width, _mode.Height, 0, format, RenderTextureReadWrite.Default);
				texture.name = "AVProDeckLink-ConvertedTexture";
				texture.wrapMode = TextureWrapMode.Clamp;
				texture.filterMode = FilterMode.Bilinear;

				if (!texture.IsCreated())
				{
					texture.enableRandomWrite = enableRandomWrite;
#if GENERATE_MIPS
					texture.useMipMap = true;
					texture.filterMode = FilterMode.Trilinear;
#if UNITY_5_5_OR_NEWER
					texture.autoGenerateMips = true;
#endif
#endif
					texture.Create();
				}
			}
		}

		private bool DoFormatConversion(RenderTexture target, Texture rawTexture)
		{
			if (target == null) return false;

			if (_isConversionMaterialDirty
#if UNITY_EDITOR && LIVE_SHADER_UPDATE
			|| true
#endif
			)
			{
				UpdateConversionMaterial();
			}

			target.DiscardContents();

			if (_conversionMethod == ConversionMethod.ComputeShader)
			{
				Debug.Assert(_computeShader != null && _computeKernel >= 0);

				_computeShader.SetTexture(_computeKernel, _propRawTexture, rawTexture);
				_computeShader.SetTexture(_computeKernel, _propResult, target);
				_computeShader.Dispatch(_computeKernel, target.width / 8, target.height / 8, 1);
			}
			else if (_conversionMethod == ConversionMethod.PixelShader)
			{
				Debug.Assert(_conversionMaterial != null);
				Graphics.Blit(rawTexture, target, _conversionMaterial, _conversionMaterialPass);
			}
			
			ValidPicture = true;

			return true;
		}

		private void DoDeinterlace(RenderTexture source, RenderTexture target)
		{
			if (_deinterlaceMethod != DeckLinkManager.Instance._deinterlaceMethod)
			{
				UpdateDeinterlaceMaterial(DeckLinkManager.Instance._deinterlaceMethod);
			}

			target.DiscardContents();
			Graphics.Blit(source, target, _deinterlaceMaterial);
		}

		private void UpdateConversionMaterial()
		{
			if (_conversionMethod == ConversionMethod.PixelShader)
			{
				if (_conversionMaterial == null) return;

				if (!_bypassGammaCorrection && QualitySettings.activeColorSpace == ColorSpace.Linear)
				{
					_conversionMaterial.EnableKeyword(ShaderApplyLinear);
				}
				else
				{
					_conversionMaterial.DisableKeyword(ShaderApplyLinear);
				}

				if (_ignoreAlphaChannel)
				{
					_conversionMaterial.EnableKeyword(ShaderKeyIgnoreAlpha);
				}
				else
				{
					_conversionMaterial.DisableKeyword(ShaderKeyIgnoreAlpha);
				}

				switch (_colorspaceMode)
				{
					case DeckLink.ColorspaceMode.Rec709:
						_conversionMaterial.DisableKeyword(ShaderKeyRec2020);
						_conversionMaterial.DisableKeyword(ShaderKeyRec2100);
						_conversionMaterial.EnableKeyword(ShaderKeyRec709);
						break;
					case DeckLink.ColorspaceMode.Rec2020:
						_conversionMaterial.DisableKeyword(ShaderKeyRec709);
						_conversionMaterial.DisableKeyword(ShaderKeyRec2100);
						_conversionMaterial.EnableKeyword(ShaderKeyRec2020);
						break;
					case DeckLink.ColorspaceMode.Rec2100:
						_conversionMaterial.DisableKeyword(ShaderKeyRec709);
						_conversionMaterial.DisableKeyword(ShaderKeyRec2020);
						_conversionMaterial.EnableKeyword(ShaderKeyRec2100);
						break;
				}

				Vector2 scale = new Vector2(1f, 1f);
				Vector2 offset = new Vector2(0f, 0f);
				if (_flipX)
				{
					scale = new Vector2(-1f, scale.y);
					offset = new Vector2(1f, offset.y);
				}
				// FlipY is flipped..due to textures being flipped
				if (!_flipY)
				{
					scale = new Vector2(scale.x, -1f);
					offset = new Vector2(offset.x, 1f);
				}

				_conversionMaterial.SetVector(_propTextureScaleOffset, new Vector4(scale.x, scale.y, offset.x, offset.y));
				_conversionMaterial.SetFloat(_propTargetTextureWidth, _mode.Width);
			}
			else if (_conversionMethod == ConversionMethod.ComputeShader)
			{
				if (_computeShader == null) return;

				if (_computeParameters == null)
				{
					_computeParameters = new ComputeBuffer(1, ComputeBufferParams.Size());
				}
				ComputeBufferParams[] p = new ComputeBufferParams[1];
				p[0].width = (uint)_eye[0].convertedTexture.width;
				p[0].height = (uint)_eye[0].convertedTexture.height;
				p[0].flipX = (uint)(_flipX?1:0);
				p[0].flipY = (uint)(_flipY?0:1);    // FlipY is flipped..due to textures being flipped
				_computeParameters.SetData(p);

				if (_mode.PixelFormat == DeckLinkPlugin.PixelFormat.YCbCr_10bpp_422 ||
					_mode.PixelFormat == DeckLinkPlugin.PixelFormat.RGB_10bpp_444)
				{
					_computeKernel = 0;
					if (!_bypassGammaCorrection && QualitySettings.activeColorSpace == ColorSpace.Linear)
					{
						_computeKernel = 1;
					}
					_computeShader.SetTexture(_computeKernel, _propRawTexture, _eye[0].rawTexture);
					_computeShader.SetTexture(_computeKernel, _propResult, _eye[0].convertedTexture);
					_computeShader.SetBuffer(_computeKernel, _propConstBuffer, _computeParameters);
				}
			}

			_isConversionMaterialDirty = false;
		}

		private void UpdateDeinterlaceMaterial(DeckLinkManager.DeinterlaceMethod method)
		{
			_deinterlaceMaterial.DisableKeyword("MODE_NONE");
			_deinterlaceMaterial.DisableKeyword("MODE_BLEND");
			_deinterlaceMaterial.DisableKeyword("MODE_DISCARD");
			_deinterlaceMaterial.DisableKeyword("MODE_DISCARDSMOOTH");

			switch (method)
			{
				case DeckLinkManager.DeinterlaceMethod.Blend:
					_deinterlaceMaterial.EnableKeyword("MODE_BLEND");
					break;
				case DeckLinkManager.DeinterlaceMethod.Discard:
					_deinterlaceMaterial.EnableKeyword("MODE_DISCARD");
					break;
				case DeckLinkManager.DeinterlaceMethod.DiscardSmooth:
					_deinterlaceMaterial.EnableKeyword("MODE_DISCARDSMOOTH");
					break;
			}

			_deinterlaceMethod = method;
		}

		public static bool InputFormatSupported(DeckLinkPlugin.PixelFormat format)
		{
			switch (format)
			{
				case DeckLinkPlugin.PixelFormat.YCbCr_8bpp_422:
				case DeckLinkPlugin.PixelFormat.YCbCr_10bpp_422:
				case DeckLinkPlugin.PixelFormat.ARGB_8bpp_444:
				case DeckLinkPlugin.PixelFormat.BGRA_8bpp_444:
				case DeckLinkPlugin.PixelFormat.RGB_10bpp_444:
					return true;
				default:
					return false;
			}
		}

		private static bool IsHighBitDepth( DeckLinkPlugin.PixelFormat format)
		{
			bool result = false;
			switch (format)
			{
				case DeckLinkPlugin.PixelFormat.YCbCr_10bpp_422:
				case DeckLinkPlugin.PixelFormat.RGB_10bpp_444:
				case DeckLinkPlugin.PixelFormat.RGBX_10bpp_444:
				case DeckLinkPlugin.PixelFormat.RGBX_10bpp_444_LE:
					result = true;
					break;
			}
			return result;
		}
	}
}

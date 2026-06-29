Shader "Hidden/PostProcess/GodRaysURP"
{
	Properties
	{
		_MainTex ("Texture", 2D) = "white" {}
		_BloomTex ("Texture", 2D) = "black" {}
		_BloomTex2 ("Texture", 2D) = "black" {}
		_GodRayTex ("Texture", 2D) = "black" {}
		_GodRayTexAlt ("Texture", 2D) = "black" {}

		_BlurTex ("Texture", 2D) = "grey" {}
		_BlurTex2 ("Texture", 2D) = "grey" {}
		_VignetteTex ("Texture", 2D) = "white" {}
	}

	HLSLINCLUDE
	
	//#include "UnityCG.cginc"
	//#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
	#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/Shaders/PostProcessing/Common.hlsl"
	
	
	TEXTURE2D_X(_MainTex);
	float4		_MainTex_ST;
	float4		_MainTex_TexelSize;
	
	TEXTURE2D_X_FLOAT(_CameraDepthTexture);
	SAMPLER(sampler_CameraDepthTexture);
	
	float3 _ViewDirTL;
	float3 _ViewDirTR;
	float3 _ViewDirBL;
	float3 _ViewDirBR;

	float3 		_SunDir;
	float3		_SunColor;
	float3 		_GodRayScreenPos;
	
	TEXTURE2D_X(_GodRayTex);
	int			_GodRaySteps;
	float 		_GodRayThreshold;
	float 		_GodRayLength;
	float 		_GodRayAmount;
	float 		_GodRaySceneContribution;
	float 		_GodRaySunContribution;
	float		_GodRayEdgeFalloff;

	// Random float from a texture coord
	float Rand(float2 coord) {
	    return frac(sin(dot(coord.xy, float2(12.9898, 78.233))) * 43758.5453);
	}

	// RGB to HSV Conversion
	half3 rgb2hsv(half3 c) {
	    half4 K = half4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
	    half4 p = lerp(half4(c.bg, K.wz), half4(c.gb, K.xy), step(c.b, c.g));
	    half4 q = lerp(half4(p.xyw, c.r), half4(c.r, p.yzx), step(p.x, c.r));
	    
	    float d = q.x - min(q.w, q.y);
	    float e = 1.0e-10;
	    return half3(abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
	}

	// HSV to RGB Conversion (for when you need to convert back)
	half3 hsv2rgb(half3 c) {
	    half4 K = half4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
	    half3 p = abs(frac(c.xxx + K.xyz) * 6.0 - K.www);
	    return c.z * lerp(K.xxx, clamp(p - K.xxx, 0.0, 1.0), c.y);
	}
	
	
	//======================================================//
	//					Composit Pass						//
	//======================================================//


	half3 SampleGodRays(float2 screenUV)
	{
		
		float2 godRayOffset = ( screenUV - _GodRayScreenPos.xy );

		// sample the god rays with a bit of dithering
		float rand = Rand(screenUV);
		float rayLength = 0.025 * (rand + 1.0);
		half4 GodRayTex = SAMPLE_TEXTURE2D_X( _BlitTexture, sampler_LinearClamp, screenUV );
		for( int i = 1; i < 4; i++ ){
			GodRayTex += SAMPLE_TEXTURE2D_X( _BlitTexture, sampler_LinearClamp, screenUV - godRayOffset * rayLength * i );
		}
		GodRayTex *= 0.25;

		// add some of the sun color to the godrays
		half3 finalGodRays = (GodRayTex.xyz * _GodRaySceneContribution);
		finalGodRays += (GodRayTex.www * _SunColor.xyz * _GodRaySunContribution);
		finalGodRays *= _GodRayAmount;

		return finalGodRays;
		
	}
	
	half4 ComposeAdd(Varyings input) : SV_Target
	{		
		
		float2 screenUV = input.texcoord;
		half3 finalGodRays = SampleGodRays(screenUV);

		return half4(finalGodRays, 0.0);

	}

	half4 ComposeScreen(Varyings input) : SV_Target
	{		
		
		float2 screenUV = input.texcoord;
		half3 finalGodRays = SampleGodRays(screenUV);
		half4 Scene = SAMPLE_TEXTURE2D_X( _MainTex, sampler_LinearClamp, screenUV );
		
		// hdr screen
		Scene.xyz = 1.0 - ( 1.0 - saturate( Scene.xyz * 0.1 ) ) * ( 1.0 - saturate( finalGodRays * 0.1 ) );
		Scene.xyz *= 10.0;

		return Scene;
	}
	
	//======================================================//
	//						Threshold						//
	//======================================================//


	// subtract the threshold from the color
	/*
	half4 SubtractThreshold(half4 col, half threshold)
	{
		col.xyz = max( col.xyz - threshold, 0);
		return col;
	}
	*/

	// subtract the threshold from the value while maintaining saturation and color
	half4 SubtractThreshold(half4 col, half threshold)
	{
		float3 colHSV = rgb2hsv(col.xyz);
		colHSV.z = saturate(colHSV.z - threshold);
		col.xyz = hsv2rgb(colHSV);
		return col;
	}

	// another way to subtract the threshold from the value while maintaining saturation and color
	/*
	half4 SubtractThreshold(half4 col, half threshold)
	{
		float value = length(col);
		if (value < threshold) return half4(0,0,0,col.w);
		float3 colNorm = normalize(col);
		value = saturate(value - threshold);
		col.xyz = colNorm * value;
		return col;
	}
	*/
	
	half4 Threshold(Varyings input) : SV_Target
	{		
		float2 halfPixelSize = _BlitTexture_TexelSize.xy * 0.5f;

		float2 coords = input.texcoord;
		float2 coord1 = coords + halfPixelSize * float2(1,1);
		float2 coord2 = coords + halfPixelSize * float2(1,-1);
		float2 coord3 = coords + halfPixelSize * float2(-1,1);
		float2 coord4 = coords + halfPixelSize * float2(-1,-1);
		
		// sample scenes
		half4 Scene1 = SAMPLE_TEXTURE2D_X( _BlitTexture, sampler_LinearClamp, coord1 );
		half4 Scene2 = SAMPLE_TEXTURE2D_X( _BlitTexture, sampler_LinearClamp, coord2 );
		half4 Scene3 = SAMPLE_TEXTURE2D_X( _BlitTexture, sampler_LinearClamp, coord3 );
		half4 Scene4 = SAMPLE_TEXTURE2D_X( _BlitTexture, sampler_LinearClamp, coord4 );
		
		float depth1 = Linear01Depth( SAMPLE_TEXTURE2D_X( _CameraDepthTexture, sampler_LinearClamp, coord1 ).x, _ZBufferParams );
		float depth2 = Linear01Depth( SAMPLE_TEXTURE2D_X( _CameraDepthTexture, sampler_LinearClamp, coord2 ).x, _ZBufferParams );
		float depth3 = Linear01Depth( SAMPLE_TEXTURE2D_X( _CameraDepthTexture, sampler_LinearClamp, coord3 ).x, _ZBufferParams );
		float depth4 = Linear01Depth( SAMPLE_TEXTURE2D_X( _CameraDepthTexture, sampler_LinearClamp, coord4 ).x, _ZBufferParams );

		// do scene stuff
		half4 Scene = ( SubtractThreshold( Scene1, _GodRayThreshold ) + SubtractThreshold( Scene2, _GodRayThreshold ) + SubtractThreshold( Scene3, _GodRayThreshold ) + SubtractThreshold( Scene4, _GodRayThreshold ) ) * 0.25;

		// sunlight volume light mask
		half depthMask = (saturate( ( depth1 - 0.99 ) * 100 ) + saturate( ( depth2 - 0.99 ) * 100 ) + saturate( ( depth3 - 0.99 ) * 100 ) + saturate( ( depth4 - 0.99 ) * 100 ) ) * 0.25;
		
		float aspect = _BlitTexture_TexelSize.w * _BlitTexture_TexelSize.x;
		float2 uvMask = coords.xy * 2.0 - 1.0;
		float2 edgeFalloff = 1.0 / float2(_GodRayEdgeFalloff * aspect, _GodRayEdgeFalloff);
		float2 screenMask = saturate( ( 1.0 - abs( uvMask ) ) * edgeFalloff );
		
		float3 viewDir = normalize(lerp( lerp( _ViewDirTL, _ViewDirTR, coords.x ), lerp( _ViewDirBL, _ViewDirBR, coords.x ), 1.0 - coords.y ));
		float sunDot = saturate( -dot( viewDir, _SunDir ) );
		
		sunDot = smoothstep( 0.8, 1.0, sunDot );
		Scene.w *= sunDot * depthMask * screenMask.x * screenMask.y;
		Scene *= Scene.w;
		
		return Scene;

	}
	
	//======================================================//
	//					Zoom Blur Passes					//
	//======================================================//
	

	half4 ZoomBlurFirst(Varyings input) : SV_Target
	{
		
		float2 screenUV = input.texcoord;
		float2 godRayOffset = ( screenUV - _GodRayScreenPos.xy );

		float4 Scene = 0;
		int i = 0;
		int passes = _GodRaySteps;
		float oneOverPasses = 1.0 / passes;
		float rayLength = _GodRayLength * oneOverPasses;
		godRayOffset *= rayLength;
		float falloff = oneOverPasses;
		float alphaACC = 0;
		for( i = 0; i < passes; i++ ){
			float alpha = 1.0 - ( falloff * i );
			alphaACC += alpha;
			Scene += SAMPLE_TEXTURE2D_X( _BlitTexture, sampler_LinearClamp, screenUV - godRayOffset * i ) * alpha;
		}
		
		Scene *= 1.0 / alphaACC;

		return Scene;
	}

	half4 ZoomBlurSecond(Varyings input) : SV_Target
	{		

		float2 screenUV = input.texcoord;
		float2 godRayOffset = ( screenUV - _GodRayScreenPos.xy );
		
		float rand = Rand(screenUV);
		
		float4 Scene = 0;
		int i = 0;
		int passes = _GodRaySteps;
		float oneOverPasses = 1.0 / passes;
		float rayLength = _GodRayLength * oneOverPasses * oneOverPasses * (rand + 1.0);
		godRayOffset *= rayLength;
		float falloff = oneOverPasses * oneOverPasses;
		float alphaACC = 0;
		for( i = 0; i < passes; i++ ){
			float alpha = 1.0 - ( falloff * (passes - i) );
			alphaACC += alpha;
			Scene += SAMPLE_TEXTURE2D_X( _BlitTexture, sampler_LinearClamp, screenUV - godRayOffset * i ) * alpha;
		}
		
		Scene *= 1.0 / alphaACC;

		return Scene;
	}

	half4 Copy(Varyings input) : SV_Target
	{		
		return SAMPLE_TEXTURE2D_X( _BlitTexture, sampler_LinearClamp, input.texcoord );
	}

	ENDHLSL

	SubShader
	{
		Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline"}
		LOD 100
		Cull Off 
		ZWrite Off 
		ZTest Always

		//Pass 0 Compose Add
		Pass
		{
			Name "Compose Add"
			
			Blend One One

			HLSLPROGRAM
			#pragma vertex Vert
			#pragma fragment ComposeAdd
			ENDHLSL
		}

		//Pass 1 Compose Screen
		Pass
		{
			Name "Compose Screen"

			HLSLPROGRAM
			#pragma vertex Vert
			#pragma fragment ComposeScreen
			ENDHLSL
		}
		
		//Pass 2 Threshold pass
		Pass 
		{
			Name "Threshold"
		
			HLSLPROGRAM
			#pragma vertex Vert
			#pragma fragment Threshold
			ENDHLSL
		}

		//Pass 3 ZoomBlur First Pass
		Pass 
		{
			Name "Zoom Blur 1"
		
			HLSLPROGRAM
			#pragma vertex Vert
			#pragma fragment ZoomBlurFirst
			ENDHLSL
		}

		//Pass 4 ZoomBlur Second Pass
		Pass 
		{
			Name "Zoom Blur 2"
		
			HLSLPROGRAM
			#pragma vertex Vert
			#pragma fragment ZoomBlurSecond
			ENDHLSL
		}

		//Pass 5 ZoomBlur Second Pass
		Pass 
		{
			Name "Copy"
		
			HLSLPROGRAM
			#pragma vertex Vert
			#pragma fragment Copy
			ENDHLSL
		}

	}
}

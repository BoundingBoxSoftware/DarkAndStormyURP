Shader "Hidden/PostProcess/GodRays"
{
	Properties
	{
		_MainTex ("Texture", 2D) = "white" {}
	}

	CGINCLUDE
	
	#include "UnityCG.cginc"

	sampler2D	_MainTex;
	float4		_MainTex_ST;
	float4		_MainTex_TexelSize;
	sampler2D_float 	_CameraDepthTexture;
	
	float3 _ViewDirTL;
	float3 _ViewDirTR;
	float3 _ViewDirBL;
	float3 _ViewDirBR;

	float3 		_SunDir;
	float3		_SunColor;
	float3 		_GodRayScreenPos;
	
	sampler2D	_GodRayTex;
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

	struct vertexData {
		float4 vertex : POSITION;
		float2 texcoord : TEXCOORD0;
	};

	struct v2f {
		float4 pos : SV_POSITION;
		float2 uv : TEXCOORD0;
		float2 uv1 : TEXCOORD1;
		float2 uv2 : TEXCOORD2;
		float2 uv3 : TEXCOORD3;
		float2 uv4 : TEXCOORD4;
		float3 viewDir : TEXCOORD5;
		
	};
	
	v2f Vert( vertexData v )
	{
		v2f o;
		o.pos = UnityObjectToClipPos (v.vertex);
		
		float2 uvCoords = v.texcoord.xy;

		o.uv = uvCoords;
		o.uv1 = uvCoords + _MainTex_TexelSize.xy * float2(-0.5,-0.5);
		o.uv2 = uvCoords + _MainTex_TexelSize.xy * float2(-0.5,0.5);
		o.uv3 = uvCoords + _MainTex_TexelSize.xy * float2(0.5,-0.5);
		o.uv4 = uvCoords + _MainTex_TexelSize.xy * float2(0.5,0.5);
		
		o.viewDir = lerp( lerp( _ViewDirTL, _ViewDirTR, uvCoords.x ), lerp( _ViewDirBL, _ViewDirBR, uvCoords.x ), 1.0 - uvCoords.y );
	
		return o;
	
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
		half4 GodRayTex = tex2D( _GodRayTex, screenUV );
		for( int i = 1; i <= 4; i++ ){
			GodRayTex += tex2Dlod( _GodRayTex, float4( screenUV - godRayOffset * rayLength * i,0,0) );
		}
		GodRayTex *= 0.25;

		// add some of the sun color to the godrays
		half3 finalGodRays = (GodRayTex.xyz * _GodRaySceneContribution);
		finalGodRays += (GodRayTex.www * _SunColor.xyz * _GodRaySunContribution);
		finalGodRays *= _GodRayAmount;

		return finalGodRays;
		
	}
	
	half4 ComposeAdd(v2f IN) : SV_Target
	{		
		
		float2 screenUV = IN.uv;
		half3 finalGodRays = SampleGodRays(screenUV);

		return half4(finalGodRays, 0.0);

	}

	half4 ComposeScreen(v2f IN) : SV_Target
	{		
		
		float2 screenUV = IN.uv;
		half3 finalGodRays = SampleGodRays(screenUV);
		half4 Scene = tex2D( _MainTex, screenUV );
		
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
	
	half4 Threshold(v2f IN) : SV_Target
	{		
		
		// sample scenes
		half4 Scene1 = tex2D( _MainTex, IN.uv1.xy );
		half4 Scene2 = tex2D( _MainTex, IN.uv2.xy );
		half4 Scene3 = tex2D( _MainTex, IN.uv3.xy );
		half4 Scene4 = tex2D( _MainTex, IN.uv4.xy );
		
		// sample depths
		float depth1 = Linear01Depth( SAMPLE_DEPTH_TEXTURE( _CameraDepthTexture, IN.uv1.xy ) );
		float depth2 = Linear01Depth( SAMPLE_DEPTH_TEXTURE( _CameraDepthTexture, IN.uv2.xy ) );
		float depth3 = Linear01Depth( SAMPLE_DEPTH_TEXTURE( _CameraDepthTexture, IN.uv3.xy ) );
		float depth4 = Linear01Depth( SAMPLE_DEPTH_TEXTURE( _CameraDepthTexture, IN.uv4.xy ) );

		// do scene stuff
		half4 Scene = ( SubtractThreshold( Scene1, _GodRayThreshold ) + SubtractThreshold( Scene2, _GodRayThreshold ) + SubtractThreshold( Scene3, _GodRayThreshold ) + SubtractThreshold( Scene4, _GodRayThreshold ) ) * 0.25;

		// sunlight depth mask
		half depthMask = ( saturate( ( depth1 - 0.99 ) * 100 ) + saturate( ( depth2 - 0.99 ) * 100 ) + saturate( ( depth3 - 0.99 ) * 100 ) + saturate( ( depth4 - 0.99 ) * 100 ) ) * 0.25;

		float aspect = _MainTex_TexelSize.w * _MainTex_TexelSize.x;
		float2 uvMask = IN.uv.xy * 2.0 - 1.0;
		float2 edgeFalloff = 1.0 / float2(_GodRayEdgeFalloff * aspect, _GodRayEdgeFalloff);
		float2 screenMask = saturate( ( 1.0 - abs( uvMask ) ) * edgeFalloff );
		
		float3 viewDir = normalize(IN.viewDir);
		float sunDot = saturate( -dot( viewDir, _SunDir ) );
		
		sunDot = smoothstep( 0.8, 1.0, sunDot );
		Scene.w *= sunDot * depthMask * screenMask.x * screenMask.y;
		Scene *= Scene.w;
		
		return Scene;
	}

	//======================================================//
	//					Zoom Blur Passes					//
	//======================================================//
	

	half4 ZoomBlurFirst(v2f IN) : SV_Target
	{
		
		float2 screenUV = IN.uv.xy;
		float2 godRayOffset = ( screenUV - _GodRayScreenPos.xy );
		
		float4 Scene = 0;
		int i = 0;
		int passes = _GodRaySteps;
		float oneOverPasses = 1.0 / passes;
		float rayLength = _GodRayLength * oneOverPasses;
		float falloff = oneOverPasses;
		float alphaACC = 0;
		for( i = 0; i < passes; i++ ){
			float alpha = 1.0 - ( falloff * i );
			alphaACC += alpha;
			Scene += tex2Dlod( _MainTex, float4( screenUV - godRayOffset * rayLength * i,0,0) ) * alpha;
		}
		
		Scene *= 1.0 / alphaACC;

		return Scene;
	}

	half4 ZoomBlurSecond(v2f IN) : SV_Target
	{		

		float2 screenUV = IN.uv.xy;
		float2 godRayOffset = ( screenUV - _GodRayScreenPos.xy );

		float rand = Rand(screenUV);
		
		float4 Scene = 0;
		int i = 0;
		int passes = _GodRaySteps;
		float oneOverPasses = 1.0 / passes;
		float rayLength = _GodRayLength * oneOverPasses * oneOverPasses * (rand + 1.0);
		float falloff = oneOverPasses * oneOverPasses;
		float alphaACC = 0;
		for( i = 0; i <= passes; i++ ){
			float alpha = 1.0 - ( falloff * i );
			alphaACC += alpha;
			Scene += tex2Dlod( _MainTex, float4( screenUV - godRayOffset * rayLength * i,0,0) );
		}
		
		Scene *= 1.0 / alphaACC;

		return Scene;
	}

	ENDCG

	SubShader
	{
		// No culling or depth
		Cull Off 
		ZWrite Off 
		ZTest Always

		//Pass 0 Compose Add
		Pass
		{
			Name "Compose Add"
			
			Blend One One

			CGPROGRAM
			#pragma vertex Vert
			#pragma fragment ComposeAdd
			#pragma target 3.0
			ENDCG
		}

		//Pass 1 Compose Screen
		Pass
		{
			Name "Compose Screen"

			CGPROGRAM
			#pragma vertex Vert
			#pragma fragment ComposeScreen
			#pragma target 3.0
			ENDCG
		}
		
		//Pass 2 Threshold pass
		Pass 
		{
			Name "Threshold"
		
			CGPROGRAM
			#pragma vertex Vert
			#pragma fragment Threshold
			#pragma target 3.0
			ENDCG
		}

		//Pass 3 ZoomBlur First Pass
		Pass 
		{
			Name "Zoom Blur 1"
		
			CGPROGRAM
			#pragma vertex Vert
			#pragma fragment ZoomBlurFirst
			#pragma target 3.0
			ENDCG
		}

		//Pass 4 ZoomBlur Second Pass
		Pass 
		{
			Name "Zoom Blur 2"
		
			CGPROGRAM
			#pragma vertex Vert
			#pragma fragment ZoomBlurSecond
			#pragma target 3.0
			ENDCG
		}

	}
}

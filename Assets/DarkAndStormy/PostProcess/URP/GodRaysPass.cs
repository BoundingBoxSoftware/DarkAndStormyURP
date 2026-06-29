using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;

public class GodRaysPass : ScriptableRenderPass
{
    static readonly string renderPassTag = "God Rays";

    private GodRaysVolume godRaysVolume;
    private Material godRaysMaterial;
    
    private MaterialPropertyBlock propertyBlockURP;
    private Material godRaysMaterialURP;

    private static ProfilingSampler thisProfilingSampler;

    //If user wants the god rays to be viewable in scene view
    bool showInSceneView = false;

    bool screenValues = false;
    
    public GodRaysPass(RenderPassEvent evt, Shader godRaysShader, Shader godRaysShaderURP, bool showInSceneView, bool screenValues)
    {
        renderPassEvent = evt;

        //to make profiling easier
        thisProfilingSampler = new ProfilingSampler(renderPassTag);

        godRaysMaterial = CoreUtils.CreateEngineMaterial(godRaysShader);
        godRaysMaterialURP = CoreUtils.CreateEngineMaterial(godRaysShaderURP);
        propertyBlockURP = new MaterialPropertyBlock();
        
        this.showInSceneView = showInSceneView;
        this.screenValues = screenValues;
    }
    
    static class Properties {
        public static int _GodRayScreenPos = Shader.PropertyToID("_GodRayScreenPos");
        public static int _SunDir = Shader.PropertyToID("_SunDir");
        public static int _SunColor = Shader.PropertyToID("_SunColor");
        public static int _ViewDirTL = Shader.PropertyToID("_ViewDirTL");
        public static int _ViewDirTR = Shader.PropertyToID("_ViewDirTR");
        public static int _ViewDirBL = Shader.PropertyToID("_ViewDirBL");
        public static int _ViewDirBR = Shader.PropertyToID("_ViewDirBR");
        public static int _GodRayTex = Shader.PropertyToID("_GodRayTex");
        
        public static int _ThresholdRT = Shader.PropertyToID("_ThresholdRT");
        public static int _Zoom1RT = Shader.PropertyToID("_Zoom1RT");
        public static int _Zoom2RT = Shader.PropertyToID("_Zoom2RT");
        public static int _CopyRT = Shader.PropertyToID("_CopyRT");
        
        public static int _MainTex = Shader.PropertyToID("_MainTex");

    }

    void SetCameraProperties(Material material, Camera camera, Light sunlight ) {
        Vector3 godRayScreenPos = camera.WorldToViewportPoint(camera.transform.position - sunlight.transform.forward * 10000.0f);
        material.SetVector(Properties._GodRayScreenPos, godRayScreenPos);
        material.SetVector(Properties._SunDir, sunlight.transform.forward);
        material.SetVector(Properties._SunColor, sunlight.color * sunlight.intensity);
            
        Vector3[] corners = new Vector3[4];
        camera.CalculateFrustumCorners(new Rect(0, 0, 1, 1), camera.farClipPlane, Camera.MonoOrStereoscopicEye.Mono, corners);
        material.SetVector(Properties._ViewDirBL, camera.transform.TransformVector(Vector3.Normalize(corners[0])) );
        material.SetVector(Properties._ViewDirTL, camera.transform.TransformVector(Vector3.Normalize(corners[1])) );
        material.SetVector(Properties._ViewDirTR, camera.transform.TransformVector(Vector3.Normalize(corners[2])) );
        material.SetVector(Properties._ViewDirBR, camera.transform.TransformVector(Vector3.Normalize(corners[3])) );
    }
    
    #if UNITY_6000_5_OR_NEWER

    private class GodRaysPassData
    {
        public TextureHandle source;
        public TextureHandle thresholdTH;
        public TextureHandle zoom1TH;
        public TextureHandle zoom2TH;
        public TextureHandle copyTH;
        public Material material;
    }
    
    // Render Graph API
    public override void RecordRenderGraph( RenderGraph renderGraph, ContextContainer frameData) {
        
        if (godRaysMaterialURP == null) {
            Debug.LogError("No God Rays URP Material");
            return;
        }

        UniversalResourceData resourcesData = frameData.Get<UniversalResourceData>();
        UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
        
        //in case if the camera doesn't have the post process option enabled and if the camera is not the game's camera
        if (cameraData.cameraType != CameraType.Game && (showInSceneView == false && cameraData.cameraType == CameraType.SceneView)) {
            return;
        }

        VolumeStack stack = VolumeManager.instance.stack;
        godRaysVolume = stack.GetComponent<GodRaysVolume>();
        if (godRaysVolume == null) return;
        
        if (!godRaysVolume.IsActive()) return;
        if( godRaysVolume.amount.value == 0f) return;
        Light sunlight = RenderSettings.sun;
        if (sunlight == null) return;
        
        // load the material settings from the volume
        godRaysVolume.Load(godRaysMaterialURP);

        var resourceData = frameData.Get<UniversalResourceData>();

        TextureHandle source = resourceData.activeColorTexture;

        var desc = renderGraph.GetTextureDesc(source);
        desc.format = UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16B16A16_SFloat;
        desc.width /= 2;
        desc.height /= 2;
        desc.name = "Threshold";
        
        TextureHandle thresholdTH = renderGraph.CreateTexture(desc);

        desc.name = "ZoomBlur1";
        desc.width /= 2;
        desc.height /= 2;

        TextureHandle zoom1TH = renderGraph.CreateTexture(desc);
        
        desc.name = "ZoomBlur2";
        
        TextureHandle zoom2TH = renderGraph.CreateTexture(desc);
        
        desc = renderGraph.GetTextureDesc(source);
        desc.name = "ScreenCopy";
        
        TextureHandle copyTH = renderGraph.CreateTexture(desc);
        
        SetCameraProperties(godRaysMaterialURP, cameraData.camera, sunlight);
        
        using (var builder = renderGraph.AddUnsafePass("God Rays Post Process", out GodRaysPassData passData)) {
        
            passData.source = source;
            passData.thresholdTH = thresholdTH;
            passData.zoom1TH = zoom1TH;
            passData.zoom2TH = zoom2TH;
            passData.copyTH = copyTH;
            passData.material = godRaysMaterialURP;
                
            builder.UseTexture(passData.source, AccessFlags.ReadWrite);
            builder.UseTexture(passData.thresholdTH, AccessFlags.ReadWrite);
            builder.UseTexture(passData.zoom1TH, AccessFlags.ReadWrite);
            builder.UseTexture(passData.zoom2TH, AccessFlags.ReadWrite);
            builder.UseTexture(passData.copyTH, AccessFlags.ReadWrite);
            
            builder.SetRenderFunc((GodRaysPassData data, UnsafeGraphContext context) =>
            {
                var cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);

                // Threshold Scene Color Pass
                Blitter.BlitCameraTexture(cmd, source, thresholdTH, godRaysMaterialURP, 2);

                // Radial Zoom BLur Pass 1
                Blitter.BlitCameraTexture(cmd, thresholdTH, zoom1TH, godRaysMaterialURP, 3);

                // Radial Zoom BLur Pass 2
                Blitter.BlitCameraTexture(cmd, zoom1TH, zoom2TH, godRaysMaterialURP, 4);

                if (screenValues) {
                    // Make a copy of the screen
                    Blitter.BlitCameraTexture(cmd, source, copyTH, godRaysMaterialURP, 5);

                    // Screen the god rays back to screen
                    godRaysMaterialURP.SetTexture(Properties._MainTex, copyTH);
                    Blitter.BlitCameraTexture(cmd, zoom2TH, source, godRaysMaterialURP, 1);
                } else {
                    // Add the god rays back to screen
                    Blitter.BlitCameraTexture(cmd, zoom2TH, source, godRaysMaterialURP, 0);
                }
            });

        }
        
    }
    
    /*
    // Render Graph API
    public override void RecordRenderGraph( RenderGraph renderGraph, ContextContainer frameData) {
        
        if (godRaysMaterialURP == null) {
            Debug.LogError("No God Rays URP Material");
            return;
        }

        UniversalResourceData resourcesData = frameData.Get<UniversalResourceData>();
        UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
        
        //in case if the camera doesn't have the post process option enabled and if the camera is not the game's camera
        if (cameraData.cameraType != CameraType.Game && (showInSceneView == false && cameraData.cameraType == CameraType.SceneView)) {
            return;
        }

        VolumeStack stack = VolumeManager.instance.stack;
        godRaysVolume = stack.GetComponent<GodRaysVolume>();
        if (godRaysVolume == null) return;
        
        if (!godRaysVolume.IsActive()) return;
        if( godRaysVolume.amount.value == 0f) return;
        Light sunlight = RenderSettings.sun;
        if (sunlight == null) return;
        
        // load the material settings from the volume
        godRaysVolume.Load(godRaysMaterialURP);

        var resourceData = frameData.Get<UniversalResourceData>();

        TextureHandle source = resourceData.activeColorTexture;

        var desc = renderGraph.GetTextureDesc(source);
        desc.format = UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16B16A16_SFloat;
        desc.width /= 2;
        desc.height /= 2;
        desc.name = "Threshold";

        TextureHandle thresholdTH = renderGraph.CreateTexture(desc);

        desc.name = "ZoomBlur1";
        desc.width /= 2;
        desc.height /= 2;

        TextureHandle zoom1TH = renderGraph.CreateTexture(desc);
        
        desc.name = "ZoomBlur2";
        
        TextureHandle zoom2TH = renderGraph.CreateTexture(desc);
        
        SetCameraProperties(godRaysMaterialURP, cameraData.camera, sunlight);

        // Threshold the colors
        RenderGraphUtils.BlitMaterialParameters threshold = new(source, thresholdTH, godRaysMaterialURP, 2);
        renderGraph.AddBlitPass( threshold, "God Rays Threshold");
        
        // Radial Zoom BLur
        renderGraph.AddBlitPass( new RenderGraphUtils.BlitMaterialParameters( thresholdTH, zoom1TH, godRaysMaterialURP, 3), "God Rays Zoom Blur First");
        renderGraph.AddBlitPass( new RenderGraphUtils.BlitMaterialParameters( zoom1TH, zoom2TH, godRaysMaterialURP, 4), "God Rays Zoom Blur Second");

        if (screenValues) {
            desc = renderGraph.GetTextureDesc(source);
            TextureHandle copyTH = renderGraph.CreateTexture(desc);

            // Make a copy of the screen
            renderGraph.AddBlitPass( new RenderGraphUtils.BlitMaterialParameters( source, copyTH, godRaysMaterialURP, 5), "Screen Copy");
            
            //godRaysMaterialURP.SetTexture(Properties._MainTex, copyTH);
            
            // Blit final result back to screen
            //renderGraph.AddBlitPass( new RenderGraphUtils.BlitMaterialParameters( zoom2TH, source, godRaysMaterialURP, 1), "God Rays Screen Composite");
            
            // gotta do a roundabout way to use more than one texture
            using (var builder = renderGraph.AddUnsafePass("God Rays Screen Composite", out CompositePassData passData)) {
                
                passData.source = zoom2TH;
                passData.sceneTexture = copyTH;
                passData.destination = source;
                
                builder.UseTexture(passData.source, AccessFlags.Read);
                builder.UseTexture(passData.sceneTexture, AccessFlags.ReadWrite);
                builder.UseTexture(passData.destination, AccessFlags.Write);
                
                builder.SetRenderFunc((CompositePassData data, UnsafeGraphContext context) =>
                {
                    var cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
                    godRaysMaterialURP.SetTexture(Properties._MainTex, copyTH);
                    //cmd.Blit(zoom2TH, source, godRaysMaterialURP, 1);
                    Blitter.BlitCameraTexture(cmd, zoom2TH, source, godRaysMaterialURP, 1);
                });
            }

        } else {
            // Blit final result back to screen
            renderGraph.AddBlitPass( new RenderGraphUtils.BlitMaterialParameters( zoom2TH, source, godRaysMaterialURP, 0), "God Rays Add Composite");
        }
        
    }
    */
    #endif
    
    #if !UNITY_6000_5_OR_NEWER
    
    // Compatibility Mode
    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {

        if (godRaysMaterial == null) {
            Debug.LogError("No God Rays Material");
            return;
        }
        
        //in case if the camera doesn't have the post process option enabled and if the camera is not the game's camera
        if (renderingData.cameraData.cameraType != CameraType.Game && (showInSceneView == false && renderingData.cameraData.cameraType == CameraType.SceneView)) {
            return;
        }

        VolumeStack stack = VolumeManager.instance.stack;
        godRaysVolume = stack.GetComponent<GodRaysVolume>();

        if (godRaysVolume == null) return;

        var cmd = CommandBufferPool.Get(renderPassTag);
        Render(cmd, context, ref renderingData);
        context.ExecuteCommandBuffer(cmd);
        cmd.Clear();

        CommandBufferPool.Release(cmd);
    }
    
    void Render(CommandBuffer cmd, ScriptableRenderContext context, ref RenderingData renderingData) {
        
        if (!godRaysVolume.IsActive()) return;
        if( godRaysVolume.amount.value == 0f) return;
        Light sunlight = RenderSettings.sun;
        if (sunlight == null) return;
        
        // load the material settings from the volume
        godRaysVolume.Load(godRaysMaterial);

        //for profiling
        using (new ProfilingScope(cmd, thisProfilingSampler))
        {
            int width = renderingData.cameraData.cameraTargetDescriptor.width;
            int height = renderingData.cameraData.cameraTargetDescriptor.height;
            
            int widthHalf = width/2;
            int heightHalf = height/2;
            
            int widthQuarter = width/4;
            int heightQuarter = height/4;
            
            SetCameraProperties(godRaysMaterial, renderingData.cameraData.camera, sunlight);
            
            // Get the source and destination render textures
            var source = renderingData.cameraData.renderer.cameraColorTargetHandle;
            var destination = renderingData.cameraData.renderer.cameraColorTargetHandle;

            cmd.GetTemporaryRT(Properties._ThresholdRT, widthHalf, heightHalf, 0, FilterMode.Bilinear, RenderTextureFormat.ARGBHalf);
            cmd.Blit(source, Properties._ThresholdRT, godRaysMaterial, 2);

            cmd.GetTemporaryRT(Properties._Zoom1RT, widthQuarter, heightQuarter, 0, FilterMode.Bilinear, RenderTextureFormat.ARGBHalf);
            cmd.GetTemporaryRT(Properties._Zoom2RT, widthQuarter, heightQuarter, 0, FilterMode.Bilinear, RenderTextureFormat.ARGBHalf);
            cmd.Blit(Properties._ThresholdRT, Properties._Zoom1RT, godRaysMaterial, 3);
            cmd.Blit(Properties._Zoom1RT, Properties._Zoom2RT, godRaysMaterial, 4);
 
            cmd.SetGlobalTexture(Properties._GodRayTex, Properties._Zoom2RT);

            if (screenValues) {
                cmd.GetTemporaryRT(Properties._CopyRT, width, height, 0, FilterMode.Point, RenderTextureFormat.DefaultHDR);
                cmd.Blit(source, Properties._CopyRT);
                cmd.Blit(Properties._CopyRT, destination, godRaysMaterial, 1);
                cmd.ReleaseTemporaryRT(Properties._CopyRT);
            } else {
                cmd.Blit(source, destination, godRaysMaterial, 0);
            }

            cmd.ReleaseTemporaryRT(Properties._ThresholdRT);
            cmd.ReleaseTemporaryRT(Properties._Zoom1RT);
            cmd.ReleaseTemporaryRT(Properties._Zoom2RT);
            
            
        }
    }

    #endif
}

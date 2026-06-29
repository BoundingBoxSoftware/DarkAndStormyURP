using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public class GodRaysRendererFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        [Tooltip("When in the rendering will this effect occure")]
        public RenderPassEvent RenderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
        [Tooltip("The shader to use for the effect")]
        public Shader shader;
        [Tooltip("The URP shader to use for the effect")]
        public Shader shaderURP;
        [Tooltip("Show the god rays in scene view")]
        public bool showInSceneView = false;
        [Tooltip("Screen or Add blend mode for god rays. Screen requires an extra copy pass.")]
        public bool screenValues = false;
    }
    
    public Settings settings = new Settings();

    GodRaysPass godRaysPass;

    //When render feature object is enabled, set the shader
    private void OnEnable() {
        Debug.Log("OnEnable");
        
        settings.shader = Shader.Find("Hidden/PostProcess/GodRays");
        settings.shaderURP = Shader.Find("Hidden/PostProcess/GodRaysURP");
    }
    
    public override void Create() {
        name = "God Rays Pass";
        if(settings.shader == null) {
            Debug.LogWarning("No God Rays Shader");
            return;
        }
        
        if(settings.shaderURP == null) {
            Debug.LogWarning("No God Rays URP Shader");
            return;
        }
        godRaysPass = new GodRaysPass(settings.RenderPassEvent, settings.shader, settings.shaderURP, settings.showInSceneView, settings.screenValues);
    }
    
    //call and adds the god rays render pass to the scriptable renderer's queue
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData) {
        renderer.EnqueuePass(godRaysPass);
    }
}

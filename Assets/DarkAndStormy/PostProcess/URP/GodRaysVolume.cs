using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;


[VolumeComponentMenu("Custom/God Rays")]
public class GodRaysVolume : VolumeComponent, IPostProcessComponent
{

    public ClampedFloatParameter amount = new ClampedFloatParameter(1f, 0f, 1f);
    public ClampedFloatParameter sceneContribution = new ClampedFloatParameter(1f, 0f, 1f);
    public ClampedFloatParameter sunContribution = new ClampedFloatParameter(1f, 0f, 1f);
    public ClampedFloatParameter threshold = new ClampedFloatParameter(1f, 0f, 1f);
    public ClampedFloatParameter edgeFalloff = new ClampedFloatParameter(0.15f, 0.01f, 1f);
    public ClampedFloatParameter length = new ClampedFloatParameter(0.8f, 0f, 1f);
    public IntParameter steps = new IntParameter(8);

    static class Properties {
        public static int _GodRayAmount = Shader.PropertyToID("_GodRayAmount");
        public static int _GodRaySceneContribution = Shader.PropertyToID("_GodRaySceneContribution");
        public static int _GodRaySunContribution = Shader.PropertyToID("_GodRaySunContribution");
        public static int _GodRayThreshold = Shader.PropertyToID("_GodRayThreshold");
        public static int _GodRayLength = Shader.PropertyToID("_GodRayLength");
        public static int _GodRaySteps = Shader.PropertyToID("_GodRaySteps");
        public static int _GodRayEdgeFalloff = Shader.PropertyToID("_GodRayEdgeFalloff");
    }
    
    public void Load(Material material) {
        material.SetFloat(Properties._GodRayAmount, amount.value);
        material.SetFloat(Properties._GodRaySceneContribution, sceneContribution.value);
        material.SetFloat(Properties._GodRaySunContribution, sunContribution.value);
        material.SetFloat(Properties._GodRayThreshold, threshold.value);
        material.SetFloat(Properties._GodRayLength, length.value);
        material.SetFloat(Properties._GodRayEdgeFalloff, edgeFalloff.value);
        material.SetInt(Properties._GodRaySteps, steps.value);
    }
    
    public bool IsActive() => true;
    
    public bool IsTileCompatible() => false;
}

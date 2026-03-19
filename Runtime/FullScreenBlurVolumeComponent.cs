using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Volume component for the full screen blur.
/// </summary>
[DisplayInfo(name = "Full Screen Blur")]
[VolumeComponentMenu("Custom/Full Screen Blur")]
[VolumeRequiresRendererFeatures(typeof(FullScreenBlurRendererFeature))]
[SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
public sealed class FullScreenBlurVolumeComponent : VolumeComponent, IPostProcessComponent
{
	#region Public Attributes

	public ClampedFloatParameter intensity = new ClampedFloatParameter(0.0f, 0.0f, 1.0f);
	public ClampedIntParameter passes = new ClampedIntParameter(2, 1, FullScreenBlurRenderPass.MaxPasses);

	#endregion

	#region IPostProcessComponent Methods

	/// <summary>
	/// <inheritdoc/>
	/// </summary>
	/// <returns></returns>
	public bool IsActive()
	{
		return intensity.value > 0.0f;
	}

	#endregion
}
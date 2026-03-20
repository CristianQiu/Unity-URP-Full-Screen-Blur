using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

/// <summary>
/// The full screen blur render pass.
/// </summary>
public sealed class FullScreenBlurRenderPass : ScriptableRenderPass
{
	#region Definitions

	/// <summary>
	/// Holds the data needed by the execution of the render pass.
	/// </summary>
	private class PassData
	{
		public TextureHandle source;

		public Material material;
		public int materialPassIndex;

		public TextureHandle[] pyramid;
		public float intensity;
		public int passes;
	}

	#endregion

	#region Public Attributes

	public const int MaxPasses = 8;

	#endregion

	#region Private Attributes

	private const float BlurIntensity = 2.0f;
	private const float BlurSizeScalingReferenceHeight = 1440.0f;

	private static readonly int IntensityId = Shader.PropertyToID("_Intensity");

	private readonly TextureHandle[] pyramid = new TextureHandle[MaxPasses];
	private Material material;

	#endregion

	#region Initialization Methods

	public FullScreenBlurRenderPass(Material material) : base()
	{
		profilingSampler = new ProfilingSampler("Full Screen Blur");
		renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
		requiresIntermediateTexture = false;

		this.material = material;
	}

	#endregion

	#region Scriptable Render Pass Methods

	/// <summary>
	/// <inheritdoc/>
	/// </summary>
	/// <param name="renderGraph"></param>
	/// <param name="frameData"></param>
	public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
	{
		UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();

		using (IUnsafeRenderGraphBuilder builder = renderGraph.AddUnsafePass("Full Screen Blur", out PassData passData, profilingSampler))
		{
			FullScreenBlurVolumeComponent volume = VolumeManager.instance.stack.GetComponent<FullScreenBlurVolumeComponent>();
			CreateRenderGraphTextures(renderGraph, resourceData, builder, volume.passes.value);

			passData.source = resourceData.activeColorTexture;
			passData.material = material;
			passData.pyramid = pyramid;
			passData.intensity = volume.intensity.value;
			passData.passes = volume.passes.value;

			builder.SetRenderAttachment(pyramid[0], 0);
			builder.UseTexture(resourceData.activeColorTexture, AccessFlags.ReadWrite);
			builder.SetRenderFunc((PassData data, UnsafeGraphContext context) => ExecuteUnsafePass(data, context));
		}
	}

	#endregion

	#region Methods

	/// <summary>
	/// Creates and returns all the necessary render graph texture handles.
	/// </summary>
	/// <param name="renderGraph"></param>
	/// <param name="resourceData"></param>
	/// <param name="builder"></param>
	/// <param name="passes"></param>
	private void CreateRenderGraphTextures(RenderGraph renderGraph, UniversalResourceData resourceData, IUnsafeRenderGraphBuilder builder, int passes)
	{
		TextureDesc descriptor = renderGraph.GetTextureDesc(resourceData.activeColorTexture);
		descriptor.name = "_FullScreenBlur";
		descriptor.clearBuffer = false;
		descriptor.depthBufferBits = 0;
		descriptor.msaaSamples = MSAASamples.None;

		for (int i = 0; i < passes; ++i)
		{
			descriptor.width = Mathf.RoundToInt((float)descriptor.width / 2.0f);
			descriptor.height = Mathf.RoundToInt((float)descriptor.height / 2.0f);

			pyramid[i] = builder.CreateTransientTexture(descriptor);
		}

		for (int i = passes; i < MaxPasses; ++i)
			pyramid[i] = TextureHandle.nullHandle;
	}

	/// <summary>
	/// Updates the material parameters according to the volume settings.
	/// </summary>
	/// <param name="material"></param>
	/// <param name="intensity"></param>
	private static void UpdateMaterialParameters(Material material, float intensity)
	{
		float blurIntensity = Mathf.Lerp(0.0f, BlurIntensity, Mathf.Sqrt(intensity));
		float factor = Screen.height / BlurSizeScalingReferenceHeight;

		// An increase of x4 pixels equals to a multiplier of 2.5 to blur intensity.
		factor = Mathf.InverseLerp(0.0f, 4.0f, factor);
		factor = Mathf.Lerp(0.0f, 2.5f, factor);

		material.SetFloat(IntensityId, blurIntensity * factor);
	}

	/// <summary>
	/// Executes the pass with the information from the pass data.
	/// </summary>
	/// <param name="passData"></param>
	/// <param name="context"></param>
	private static void ExecuteUnsafePass(PassData passData, UnsafeGraphContext context)
	{
		Material material = passData.material;
		UpdateMaterialParameters(material, passData.intensity);

		CommandBuffer cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
		Blitter.BlitTexture(cmd, passData.source, Vector2.one, material, 0);

		int passes = passData.passes;
		for (int i = 0; i < (passes - 1); ++i)
			Blitter.BlitCameraTexture(cmd, passData.pyramid[i], passData.pyramid[i + 1], material, 0);

		for (int i = (passes - 1); i > 0; --i)
			Blitter.BlitCameraTexture(cmd, passData.pyramid[i], passData.pyramid[i - 1], material, 1);

		Blitter.BlitCameraTexture(cmd, passData.pyramid[0], passData.source, material, 1);
	}

	#endregion
}
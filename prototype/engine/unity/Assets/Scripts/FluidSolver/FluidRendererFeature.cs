using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Liquid.FluidSolver
{
    public class FluidRendererFeature : ScriptableRendererFeature
    {
        private FluidDepthPass depthPass;
        private FluidThicknessPass thicknessPass;
        private FluidSmoothDepthPass smoothDepthPass;
        private FluidCompositePass compositePass;

        public override void Create()
        {
            depthPass = new FluidDepthPass();
            depthPass.renderPassEvent = RenderPassEvent.AfterRenderingOpaques;

            thicknessPass = new FluidThicknessPass();
            thicknessPass.renderPassEvent = RenderPassEvent.AfterRenderingOpaques;

            smoothDepthPass = new FluidSmoothDepthPass();
            smoothDepthPass.renderPassEvent = RenderPassEvent.AfterRenderingOpaques;

            compositePass = new FluidCompositePass();
            compositePass.renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (FlipSolver.Instance == null || FlipSolver.Instance.ParticleCount <= 0)
                return;

            renderer.EnqueuePass(depthPass);
            renderer.EnqueuePass(thicknessPass);
            renderer.EnqueuePass(smoothDepthPass);
            renderer.EnqueuePass(compositePass);
        }

        protected override void Dispose(bool disposing)
        {
            depthPass?.Cleanup();
            thicknessPass?.Cleanup();
            smoothDepthPass?.Cleanup();
            compositePass?.Cleanup();
        }
    }

    class FluidDepthPass : ScriptableRenderPass
    {
        private Material material;
        private RTHandle depthRT;

        public FluidDepthPass()
        {
            profilingSampler = new ProfilingSampler("FluidDepth");
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            var desc = renderingData.cameraData.cameraTargetDescriptor;
            desc.colorFormat = RenderTextureFormat.RFloat;
            desc.depthBufferBits = 24;
            desc.msaaSamples = 1;
            RenderingUtils.ReAllocateIfNeeded(ref depthRT, desc, FilterMode.Point, name: "_FluidDepthTex");
            ConfigureTarget(depthRT);
            ConfigureClear(ClearFlag.All, Color.clear);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (material == null)
            {
                var shader = Shader.Find("Hidden/FlipParticleDepth");
                if (shader == null) return;
                material = new Material(shader);
            }

            var solver = FlipSolver.Instance;
            CommandBuffer cmd = CommandBufferPool.Get("FluidDepth");

            material.SetBuffer("_ParticleBuf", solver.ParticleBuffer);
            material.SetFloat("_ParticleRadius", 0.015f);
            material.SetMatrix("_LocalToWorld", solver.transform.localToWorldMatrix);

            cmd.DrawProcedural(Matrix4x4.identity, material, 0, MeshTopology.Triangles, 6 * solver.ParticleCount);

            cmd.SetGlobalTexture("_FluidDepthTex", depthRT);
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public void Cleanup() { depthRT?.Release(); if (material) Object.DestroyImmediate(material); }
    }

    class FluidThicknessPass : ScriptableRenderPass
    {
        private Material material;
        private RTHandle thicknessRT;

        public FluidThicknessPass()
        {
            profilingSampler = new ProfilingSampler("FluidThickness");
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            var desc = renderingData.cameraData.cameraTargetDescriptor;
            desc.colorFormat = RenderTextureFormat.RFloat;
            desc.depthBufferBits = 0;
            desc.msaaSamples = 1;
            RenderingUtils.ReAllocateIfNeeded(ref thicknessRT, desc, FilterMode.Bilinear, name: "_FluidThicknessTex");
            ConfigureTarget(thicknessRT);
            ConfigureClear(ClearFlag.Color, Color.clear);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (material == null)
            {
                var shader = Shader.Find("Hidden/FlipParticleThickness");
                if (shader == null) return;
                material = new Material(shader);
            }

            var solver = FlipSolver.Instance;
            CommandBuffer cmd = CommandBufferPool.Get("FluidThickness");

            material.SetBuffer("_ParticleBuf", solver.ParticleBuffer);
            material.SetFloat("_ParticleRadius", 0.015f);
            material.SetMatrix("_LocalToWorld", solver.transform.localToWorldMatrix);

            cmd.DrawProcedural(Matrix4x4.identity, material, 0, MeshTopology.Triangles, 6 * solver.ParticleCount);

            cmd.SetGlobalTexture("_FluidThicknessTex", thicknessRT);
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public void Cleanup() { thicknessRT?.Release(); if (material) Object.DestroyImmediate(material); }
    }

    class FluidSmoothDepthPass : ScriptableRenderPass
    {
        private Material material;
        private RTHandle tempRT;
        private RTHandle smoothedRT;

        public FluidSmoothDepthPass()
        {
            profilingSampler = new ProfilingSampler("FluidSmoothDepth");
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            var desc = renderingData.cameraData.cameraTargetDescriptor;
            desc.colorFormat = RenderTextureFormat.RFloat;
            desc.depthBufferBits = 0;
            desc.msaaSamples = 1;
            RenderingUtils.ReAllocateIfNeeded(ref tempRT, desc, FilterMode.Point, name: "_FluidDepthTemp");
            RenderingUtils.ReAllocateIfNeeded(ref smoothedRT, desc, FilterMode.Bilinear, name: "_FluidSmoothedDepthTex");
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (material == null)
            {
                var shader = Shader.Find("Hidden/FlipSmoothDepth");
                if (shader == null) return;
                material = new Material(shader);
            }

            CommandBuffer cmd = CommandBufferPool.Get("FluidSmoothDepth");

            var desc = renderingData.cameraData.cameraTargetDescriptor;
            Vector4 texelSize = new Vector4(1.0f / desc.width, 1.0f / desc.height, desc.width, desc.height);
            cmd.SetGlobalVector("_FluidDepthTex_TexelSize", texelSize);

            // Horizontal pass: _FluidDepthTex -> tempRT
            material.SetVector("_BlurDir", new Vector4(1, 0, 0, 0));
            cmd.SetRenderTarget(tempRT);
            cmd.DrawProcedural(Matrix4x4.identity, material, 0, MeshTopology.Triangles, 3);

            cmd.SetGlobalTexture("_FluidDepthTex", tempRT);

            // Vertical pass: tempRT -> smoothedRT
            material.SetVector("_BlurDir", new Vector4(0, 1, 0, 0));
            cmd.SetRenderTarget(smoothedRT);
            cmd.DrawProcedural(Matrix4x4.identity, material, 0, MeshTopology.Triangles, 3);

            cmd.SetGlobalTexture("_FluidSmoothedDepthTex", smoothedRT);
            cmd.SetGlobalVector("_FluidSmoothedDepthTex_TexelSize", texelSize);
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public void Cleanup() { tempRT?.Release(); smoothedRT?.Release(); if (material) Object.DestroyImmediate(material); }
    }

    class FluidCompositePass : ScriptableRenderPass
    {
        private Material material;

        public FluidCompositePass()
        {
            profilingSampler = new ProfilingSampler("FluidComposite");
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            ConfigureTarget(renderingData.cameraData.renderer.cameraColorTargetHandle);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (material == null)
            {
                var shader = Shader.Find("Hidden/FlipFluidComposite");
                if (shader == null) return;
                material = new Material(shader);
            }

            CommandBuffer cmd = CommandBufferPool.Get("FluidComposite");

            cmd.DrawProcedural(Matrix4x4.identity, material, 0, MeshTopology.Triangles, 3);

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public void Cleanup() { if (material) Object.DestroyImmediate(material); }
    }
}

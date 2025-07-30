using UnityEngine;

namespace Kino
{
    public partial class Motion
    {
        // Reconstruction filter for shutter speed simulation
        public class ReconstructionFilter
        {
            #region Predefined constants

            // The maximum length of motion blur, given as a percentage
            // of the screen height. Larger values may introduce artifacts.
            //default 5
            const float kMaxBlurRadius = 5;

            #endregion

            #region Public methods

            public ReconstructionFilter(Shader shader)
            {
                //var shader = Shader.Find("Hidden/Kino/Motion/Reconstruction");
                //if (shader.isSupported && CheckTextureFormatSupport()) {
                _material = new Material(shader);
                _material.hideFlags = HideFlags.DontSave;
                //}
            }

            public void Release()
            {
                if (_material != null) DestroyImmediate(_material);
                _material = null;
            }

            public void ProcessImage(
                float shutterAngle, int sampleCount,
                RenderTexture source, RenderTexture destination
            )
            {
                // If the shader isn't supported, simply blit and return.
                if (_material == null) {
                    Graphics.Blit(source, destination);
                    return;
                }

                // Calculate the maximum blur radius in pixels.
                var maxBlurPixels = (int)(kMaxBlurRadius * source.height / 100);

                // Calculate the TileMax size.
                // It should be a multiple of 8 and larger than maxBlur.
                var tileSize = ((maxBlurPixels - 1) / 8 + 1) * 8;

                // 1st pass - Velocity/depth packing
                var velocityScale = shutterAngle / 360;
                _material.SetFloat("_VelocityScale", velocityScale);
                _material.SetFloat("_MaxBlurRadius", maxBlurPixels);
                _material.SetFloat("_RcpMaxBlurRadius", 1.0f / maxBlurPixels);

                var vbuffer = GetTemporaryRT(source, 1, _packedRTFormat);
                Graphics.Blit(null, vbuffer, _material, 0);

                // 2nd pass - 1/2 TileMax filter
                var tile2 = GetTemporaryRT(source, 2, _vectorRTFormat);
                Graphics.Blit(vbuffer, tile2, _material, 1);

                // 3rd pass - 1/2 TileMax filter
                var tile4 = GetTemporaryRT(source, 4, _vectorRTFormat);
                Graphics.Blit(tile2, tile4, _material, 2);
                ReleaseTemporaryRT(tile2);

                // 4th pass - 1/2 TileMax filter
                var tile8 = GetTemporaryRT(source, 8, _vectorRTFormat);
                Graphics.Blit(tile4, tile8, _material, 2);
                ReleaseTemporaryRT(tile4);

                // 5th pass - Last TileMax filter (reduce to tileSize)
                var tileMaxOffs = Vector2.one * (tileSize / 8.0f - 1) * -0.5f;
                _material.SetVector("_TileMaxOffs", tileMaxOffs);
                _material.SetInt("_TileMaxLoop", tileSize / 8);

                var tile = GetTemporaryRT(source, tileSize, _vectorRTFormat);
                Graphics.Blit(tile8, tile, _material, 3);
                ReleaseTemporaryRT(tile8);

                // 6th pass - NeighborMax filter
                var neighborMax = GetTemporaryRT(source, tileSize, _vectorRTFormat);
                Graphics.Blit(tile, neighborMax, _material, 4);
                ReleaseTemporaryRT(tile);

                // 7th pass - Reconstruction pass
                _material.SetFloat("_LoopCount", Mathf.Clamp(sampleCount, 2, 10000000) / 2);
                _material.SetTexture("_NeighborMaxTex", neighborMax);
                _material.SetTexture("_VelocityTex", vbuffer);
                Graphics.Blit(source, destination, _material, 5);

                // Cleaning up
                ReleaseTemporaryRT(vbuffer);
                ReleaseTemporaryRT(neighborMax);
            }

            #endregion

            #region Private members

            public Material _material;

            // Texture format for storing 2D vectors.
            RenderTextureFormat _vectorRTFormat = RenderTextureFormat.RGHalf;

            // Texture format for storing packed velocity/depth.
            RenderTextureFormat _packedRTFormat = RenderTextureFormat.ARGB2101010;

            bool CheckTextureFormatSupport()
            {
                // RGHalf is not supported = Can't use motion vectors.
                if (!SystemInfo.SupportsRenderTextureFormat(_vectorRTFormat))
                    return false;

                // If 2:10:10:10 isn't supported, use ARGB32 instead.
                if (!SystemInfo.SupportsRenderTextureFormat(_packedRTFormat))
                    _packedRTFormat = RenderTextureFormat.ARGB32;

                return true;
            }

            RenderTexture GetTemporaryRT(
                Texture source, int divider, RenderTextureFormat format
            )
            {
                var w = source.width / divider;
                var h = source.height / divider;
                var linear = RenderTextureReadWrite.Linear;
                var rt = RenderTexture.GetTemporary(w, h, 0, format, linear);
                rt.filterMode = FilterMode.Point;
                return rt;
            }

            public void ReleaseTemporaryRT(RenderTexture rt)
            {
                RenderTexture.ReleaseTemporary(rt);
            }

            #endregion
        }
    }
}

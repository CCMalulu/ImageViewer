﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ImageFramework.DirectX;
using ImageFramework.ImageLoader;
using ImageFramework.Model.Shader;
using ImageFramework.Utility;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using SharpDX.Mathematics.Interop;
using Device = ImageFramework.DirectX.Device;

namespace ImageFramework.Model
{
    /// <summary>
    /// responsible for creating thumbnails
    /// </summary>
    public class ThumbnailModel : IDisposable
    {
        private readonly QuadShader quad;
        private readonly DirectX.Shader convert;
        private readonly SamplerState sampler;

        public ThumbnailModel(QuadShader quad)
        {
            this.quad = quad;
            convert = new DirectX.Shader(DirectX.Shader.Type.Pixel, GetSource(), "ThumbnailPixelShader");
            var samplerDesc = new SamplerStateDescription
            {
                AddressU = TextureAddressMode.Clamp,
                AddressV = TextureAddressMode.Clamp,
                AddressW = TextureAddressMode.Clamp,
                BorderColor = new RawColor4(),
                ComparisonFunction = Comparison.Never,
                Filter = SharpDX.Direct3D11.Filter.MinLinearMagMipPoint,
                MaximumAnisotropy = 1,
                MaximumLod = float.MaxValue,
                MinimumLod = 0,
                MipLodBias = 0
            };
            sampler = new SamplerState(Device.Get().Handle, samplerDesc);
        }

        /// <summary>
        /// creates a thumbnail for one image layer/mipmap
        /// </summary>
        /// <param name="size">maximum width/height of the thumbnail</param>
        /// <param name="texture">source texture</param>
        /// <param name="dstFormat">destination texture format</param>
        /// <param name="layer">source layer</param>
        /// <returns>texture with width, height smaller or equal to size. One layer and one mipmap</returns>
        public TextureArray2D CreateThumbnail(int size, TextureArray2D texture,
            SharpDX.DXGI.Format dstFormat, int layer)
        {
            Debug.Assert(ImageFormat.IsSupported(dstFormat));
            Debug.Assert(ImageFormat.IsSupported(texture.Format));

            // determine dimensions of output texture
            var width = 0;
            var height = 0;
            if (texture.Size.Width > texture.Size.Height)
            {
                width = size;
                height = (texture.Size.Height * size) / texture.Size.Width;
            }
            else
            {
                height = size;
                width = (texture.Size.Width * size) / texture.Size.Height;
            }
            Debug.Assert(width <= size);
            Debug.Assert(height <= size);

            var res = new TextureArray2D(1, 1, new Size3(width, height), dstFormat, false);

            var dev = Device.Get();
            quad.Bind(false);
            dev.Pixel.Set(convert.Pixel);
            
            // compute which mipmap has the closest fit
            var mipmap = 0;
            var curWidth = texture.Size.Width;
            while (curWidth >= width)
            {
                ++mipmap;
                curWidth /= 2;
            }
            // mipmap just jumped over the optimal size
            mipmap = Math.Max(0, mipmap - 1);

            TextureArray2D tmpTex = null;
            if (texture.NumMipmaps < mipmap + 1)
            {
                // generate new texture with mipmaps
                tmpTex = texture.GenerateMipmapLevelsT(mipmap + 1);
                dev.Pixel.SetShaderResource(0, tmpTex.GetSrView(layer, mipmap));
            }
            else
            {
                dev.Pixel.SetShaderResource(0, texture.GetSrView(layer, mipmap));
            }

            dev.Pixel.SetSampler(0, sampler);

            dev.OutputMerger.SetRenderTargets(res.GetRtView(0, 0));
            dev.SetViewScissors(width, height);
            dev.DrawQuad();

            // remove bindings
            dev.Pixel.SetShaderResource(0, null);
            dev.OutputMerger.SetRenderTargets((RenderTargetView)null);
            quad.Unbind();

            tmpTex?.Dispose();

            return res;
        }

        private static string GetSource()
        {
            return @"
Texture2D<float4> tex : register(t0);
SamplerState texSampler : register(s0);

struct PixelIn
{
    float2 texcoord : TEXCOORD;
    float4 projPos : SV_POSITION;
};

float4 main(PixelIn i) : SV_TARGET
{
    float4 color = tex.Sample(texSampler, i.texcoord);
    return color;
}
";
        }

        public void Dispose()
        {
            quad?.Dispose();
            convert?.Dispose();
            sampler?.Dispose();
        }
    }
}

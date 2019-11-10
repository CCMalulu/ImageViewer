﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ImageFramework.DirectX;
using ImageFramework.DirectX.Structs;
using ImageFramework.ImageLoader;
using ImageFramework.Utility;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using Device = SharpDX.Direct3D11.Device;
using Resource = ImageFramework.ImageLoader.Resource;

namespace ImageFramework.Model.Shader
{
    public class ConvertFormatShader : IDisposable
    {
        private readonly DirectX.Shader convert2D;
        private readonly DirectX.Shader convert3D;
        private readonly QuadShader quad;
        private readonly UploadBuffer<LayerLevelOffsetData> cbuffer;

        public ConvertFormatShader(QuadShader quad)
        {
            var dev = DirectX.Device.Get();
            convert2D = new DirectX.Shader(DirectX.Shader.Type.Pixel, GetSource(ShaderBuilder.Builder2D), "ConvertFormatShader2D");
            convert3D = new DirectX.Shader(DirectX.Shader.Type.Pixel, GetSource(ShaderBuilder.Builder3D), "ConvertFormatShader3D");
            this.quad = quad;
            cbuffer = new UploadBuffer<LayerLevelOffsetData>(1);
        }

        /// <summary>
        /// converts the texture into another format and performs cropping if requested
        /// </summary>
        /// <param name="texture">source texture</param>
        /// <param name="dstFormat">destination format</param>
        /// <param name="mipmap">mipmap to export, -1 for all mipmaps</param>
        /// <param name="layer">layer to export, -1 for all layers</param>
        /// <param name="multiplier">rgb channels will be multiplied by this value</param>
        public ITexture Convert(ITexture texture, SharpDX.DXGI.Format dstFormat, int mipmap = -1, int layer = -1, float multiplier = 1.0f)
       {
            return Convert(texture, dstFormat, mipmap, layer, multiplier, false, Size3.Zero, Size3.Zero, Size3.Zero);
       }

        /// <summary>
        /// converts the texture into another format and performs cropping if requested
        /// </summary>
        /// <param name="texture">source texture</param>
        /// <param name="dstFormat">destination format</param>
        /// <param name="mipmap">mipmap to export, -1 for all mipmaps</param>
        /// <param name="layer">layer to export, -1 for all layers</param>
        /// <param name="multiplier">rgb channels will be multiplied by this value</param>
        /// <param name="crop">indicates if the image should be cropped, only works with 1 mipmap to export</param>
        /// <param name="offset">if crop: offset in source image</param>
        /// <param name="size">if crop: size of the destination image</param>
        /// <param name="align">if nonzero: texture width will be aligned to this (rounded down)</param>
        /// <returns></returns>
        public ITexture Convert(ITexture texture, SharpDX.DXGI.Format dstFormat, int mipmap, int layer, float multiplier, bool crop, 
            Size3 offset, Size3 size, Size3 align)
        {
            Debug.Assert(ImageFormat.IsSupported(dstFormat));
            Debug.Assert(ImageFormat.IsSupported(texture.Format));

            // set width, height mipmap
            int firstMipmap = Math.Max(mipmap, 0);
            int firstLayer = Math.Max(layer, 0);
            int nMipmaps = mipmap == -1 ? texture.NumMipmaps : 1;
            int nLayer = layer == -1 ? texture.NumLayers : 1;

            // set correct width, height, offsets
            if (!crop)
            {
                size = texture.Size.GetMip(firstMipmap);
                offset = Size3.Zero;
            }

            // adjust alignments
            for (int i = 0; i < 3; ++i)
            {
                if (align[i] != 0)
                {
                    if (size[i] % align[i] != 0)
                    {
                        if (size[i] < align[i])
                            throw new Exception($"image needs to be aligned to {align[i]} but one axis is only {size[i]}. Axis should be at least {align[i]}");

                        crop = true;
                        var remainder = size[i] % align[i];
                        offset[i] = offset[i] + remainder / 2;
                        size[i] = size[i] - remainder;
                    }
                }
            }

            bool recomputeMips = nMipmaps > 1 && crop;
            if (recomputeMips)
            {
                // number of mipmaps might have changed
                nMipmaps = ImagesModel.ComputeMaxMipLevels(size);
                recomputeMips = nMipmaps > 1;
            }

            var res = texture.Create(nLayer, nMipmaps, size, dstFormat, false);

            var dev = DirectX.Device.Get();
            quad.Bind(texture.Is3D);
            if(texture.Is3D) dev.Pixel.Set(convert3D.Pixel);
            else dev.Pixel.Set(convert2D.Pixel);

            dev.Pixel.SetShaderResource(0, texture.View);

            for (int curLayer = 0; curLayer < nLayer; ++curLayer)
            {
                for (int curMipmap = 0; curMipmap < nMipmaps; ++curMipmap)
                {
                    cbuffer.SetData(new LayerLevelOffsetData
                    {
                        Layer = curLayer + firstLayer + offset.Z,
                        Level = curMipmap + firstMipmap,
                        Xoffset = offset.X,
                        Yoffset = offset.Y,
                        Multiplier = multiplier
                    });

                    var dim = res.Size.GetMip(curMipmap);
                    dev.Pixel.SetConstantBuffer(0, cbuffer.Handle);
                    dev.OutputMerger.SetRenderTargets(res.GetRtView(curLayer, curMipmap));
                    dev.SetViewScissors(dim.Width, dim.Height);
                    dev.DrawFullscreenTriangle(dim.Depth);

                    if(recomputeMips) break; // only write most detailed mipmap
                }
            }

            // remove bindings
            dev.Pixel.SetShaderResource(0, null);
            dev.OutputMerger.SetRenderTargets((RenderTargetView) null);
            quad.Unbind();

            if (recomputeMips)
            {
                res.RegenerateMipmapLevels();
            }

            return res;
        }

        /// <summary>
        /// for unit testing purposes. Converts naked srv to TextureArray2D
        /// </summary>
        internal TextureArray2D ConvertFromRaw(SharpDX.Direct3D11.ShaderResourceView srv, Size3 size, SharpDX.DXGI.Format dstFormat)
        {
            var res = new TextureArray2D(1, 1, size, dstFormat, false);

            var dev = DirectX.Device.Get();
            quad.Bind(false);
            dev.Pixel.Set(convert2D.Pixel);

            dev.Pixel.SetShaderResource(0, srv);

            cbuffer.SetData(new LayerLevelOffsetData
            {
                Layer = 0,
                Level = 0,
                Xoffset = 0,
                Yoffset = 0,
                Multiplier = 1.0f
            });

            dev.Pixel.SetConstantBuffer(0, cbuffer.Handle);
            dev.OutputMerger.SetRenderTargets(res.GetRtView(0, 0));
            dev.SetViewScissors(size.Width, size.Height);
            dev.DrawQuad();

            // remove bindings
            dev.Pixel.SetShaderResource(0, null);
            dev.OutputMerger.SetRenderTargets((RenderTargetView)null);
            quad.Unbind();

            return res;
        }

        private static string GetSource(IShaderBuilder builder)
        {
            return $@"
{builder.SrvType} in_tex : register(t0);

cbuffer InfoBuffer : register(b0)
{{
    uint layer; // contains offset z for 3d textures
    uint level;
    uint xoffset;
    uint yoffset;
    float multiplier;
}};

struct PixelIn
{{
    float2 texcoord : TEXCOORD;
    float4 projPos : SV_POSITION;
    uint depth : SV_RenderTargetArrayIndex;
}};

float4 main(PixelIn i) : SV_TARGET
{{
    float4 coord = i.projPos;
    return multiplier * in_tex.mips[level][uint3(xoffset + uint(coord.x), yoffset + uint(coord.y), layer + i.depth)];
}}
";
        }

        public void Dispose()
        {
            convert2D?.Dispose();
            convert3D?.Dispose();
            cbuffer?.Dispose();
        }
    }
}

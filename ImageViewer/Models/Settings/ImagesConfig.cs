﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ImageFramework.DirectX;
using ImageFramework.ImageLoader;
using ImageFramework.Utility;
using ImageViewer.Controller;
using Microsoft.SqlServer.Server;
using Format = SharpDX.DXGI.Format;

namespace ImageViewer.Models.Settings
{
    public class ImagesConfig
    {
        public class ImageData
        {
            public bool DataIsFilename { get; set; } // otherwise
            public string Data { get; set; }
            public string Alias { get; set; }
        }

        public List<ImageData> Images { get; set; } = new List<ImageData>();

        public int NumMipmaps { get; set; }
        public int NumLayers { get; set; }

        public int Width { get; set; }
        public int Height { get; set; }
        public int Depth { get; set; }

        public ViewerConfig.ImportMode ImportMode { get; set; }

        public static ImagesConfig LoadFromModels(ModelsEx models)
        {
            var res = new ImagesConfig();
            res.NumLayers = models.Images.NumLayers;
            res.NumMipmaps = models.Images.NumMipmaps;
            res.Width = models.Images.Size.Width;
            res.Height = models.Images.Size.Height;
            res.Depth = models.Images.Size.Depth;

            foreach (var img in models.Images.Images)
            {
                if(img.IsFile)
                {
                    res.Images.Add(new ImageData
                    {
                        DataIsFilename = true,
                        Data = img.Filename,
                        Alias = img.Alias
                    });
                }
                else
                {
                    Debug.Assert(img.Image.Format == Format.R32G32B32A32_Float);

                    // fill byte array
                    var bytes = img.Image.GetBytes(4u * 4u);
                    bytes = Compression.Compress(bytes, CompressionLevel.Fastest);

                    var base64 = System.Convert.ToBase64String(bytes);

                    res.Images.Add(new ImageData
                    {
                        DataIsFilename = false,
                        Data = base64,
                        Alias = img.Alias
                    });
                }
            }

            return res;
        }

        public async Task ApplyToModels(ModelsEx models)
        {
            // clear images
            if(ImportMode == ViewerConfig.ImportMode.Replace)
                models.Images.Clear();

            var import = new ImportDialogController(models);
            var layerMipmaps = new LayerMipmapCount(NumLayers, NumMipmaps);
            var imgSize = new Size3(Width, Height, Depth);

            // add images from config
            foreach (var img in Images)
            {
                if(img.DataIsFilename)
                    await import.ImportImageAsync(img.Data, img.Alias);
                else
                {
                    // load base 64 bytes
                    var bytes = System.Convert.FromBase64String(img.Data);
                    bytes = Compression.Decompress(bytes);
                    var bi = new ByteImageData(bytes, layerMipmaps, imgSize, new ImageFormat(Format.R32G32B32A32_Float));
                    ITexture tex = null;
                    if (bi.Is3D) tex = new Texture3D(bi);
                    else tex = new TextureArray2D(bi);

                    try
                    {
                        models.Images.AddImage(tex, "imported", GliFormat.RGBA32_SFLOAT, img.Alias);
                    }
                    catch (Exception e)
                    {
                        tex?.Dispose();
                        models.Window.ShowErrorDialog(e);
                    }
                }
            }
        }
    }
}

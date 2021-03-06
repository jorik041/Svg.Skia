﻿// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using System;
using SkiaSharp;

namespace Svg.Skia
{
    public class SKSvg : IDisposable
    {
        public SKPicture? Picture { get; set; }

        internal SKPicture Load(SvgFragment svgFragment)
        {
            var skSize = SkiaUtil.GetDimensions(svgFragment);
            var cullRect = SKRect.Create(skSize);
            using (var skPictureRecorder = new SKPictureRecorder())
            using (var skCanvas = skPictureRecorder.BeginRecording(cullRect))
            using (var renderer = new SKSvgRenderer(skCanvas, skSize))
            {
                renderer.DrawFragment(svgFragment, false);
                return skPictureRecorder.EndRecording();
            }
        }

        public SKPicture? Load(System.IO.Stream stream)
        {
            Reset();
            var svgDocument = SvgDocument.Open<SvgDocument>(stream, null);
            if (svgDocument != null)
            {
                svgDocument.FlushStyles(true);
                Picture = Load(svgDocument);
                return Picture;
            }
            return null;
        }

        public SKPicture? Load(string path)
        {
            Reset();
            var extension = System.IO.Path.GetExtension(path);
            switch (extension.ToLower())
            {
                default:
                case ".svg":
                    {
                        var svgDocument = SvgDocument.Open<SvgDocument>(path, null);
                        if (svgDocument != null)
                        {
                            svgDocument.FlushStyles(true);
                            Picture = Load(svgDocument);
                            return Picture;
                        }
                        return null;
                    }
                case ".svgz":
                    {
                        using (var fileStream = System.IO.File.OpenRead(path))
                        using (var gzipStream = new System.IO.Compression.GZipStream(fileStream, System.IO.Compression.CompressionMode.Decompress))
                        using (var memoryStream = new System.IO.MemoryStream())
                        {
                            gzipStream.CopyTo(memoryStream);
                            memoryStream.Position = 0;
                            return Load(memoryStream);
                        }
                    }
            }
        }

        public SKPicture? FromSvg(string svg)
        {
            Reset();
            var svgDocument = SvgDocument.FromSvg<SvgDocument>(svg);
            if (svgDocument != null)
            {
                svgDocument.FlushStyles(true);
                Picture = Load(svgDocument);
                return Picture;
            }
            return null;
        }

        public SKPicture? FromSvgDocument(SvgDocument svgDocument)
        {
            Reset();
            if (svgDocument != null)
            {
                svgDocument.FlushStyles(true);
                Picture = Load(svgDocument);
                return Picture;
            }
            return null;
        }

        public bool Save(System.IO.Stream stream, SKColor background, SKEncodedImageFormat format = SKEncodedImageFormat.Png, int quality = 100, float scaleX = 1f, float scaleY = 1f)
        {
            if (Picture == null)
            {
                return false;
            }

            float width = Picture.CullRect.Width * scaleX;
            float height = Picture.CullRect.Height * scaleY;

            if (width > 0 && height > 0)
            {
                var skImageInfo = new SKImageInfo((int)width, (int)height);
                using (var skBitmap = new SKBitmap(skImageInfo))
                using (var skCanvas = new SKCanvas(skBitmap))
                {
                    skCanvas.Clear(SKColors.Transparent);
                    if (background != SKColor.Empty)
                    {
                        skCanvas.DrawColor(background);
                    }
                    skCanvas.Save();
                    skCanvas.Scale(scaleX, scaleY);
                    skCanvas.DrawPicture(Picture);
                    skCanvas.Restore();
                    using (var skImage = SKImage.FromBitmap(skBitmap))
                    using (var skData = skImage.Encode(format, quality))
                    {
                        if (skData != null)
                        {
                            skData.SaveTo(stream);
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        public bool Save(string path, SKColor background, SKEncodedImageFormat format = SKEncodedImageFormat.Png, int quality = 100, float scaleX = 1f, float scaleY = 1f)
        {
            using (var stream = System.IO.File.OpenWrite(path))
            {
                return Save(stream, background, format, quality, scaleX, scaleY);
            }
        }

        public void Reset()
        {
            if (Picture != null)
            {
                Picture.Dispose();
                Picture = null;
            }
        }

        public void Dispose()
        {
            Reset();
        }
    }
}

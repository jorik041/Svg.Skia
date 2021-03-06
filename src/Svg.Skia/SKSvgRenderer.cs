﻿// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
// Parts of this source file are adapted from the https://github.com/vvvv/SVG
using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using SkiaSharp;
using Svg.DataTypes;
using Svg.Document_Structure;

namespace Svg.Skia
{
    public class SKSvgRenderer : ISvgRenderer
    {
        private readonly SKCanvas _skCanvas;
        private readonly SKSize _skSize;
        private readonly CompositeDisposable _disposable;

        public SKSvgRenderer(SKCanvas skCanvas, SKSize skSize)
        {
            _skCanvas = skCanvas;
            _skSize = skSize;
            _disposable = new CompositeDisposable();
        }

        public void Dispose()
        {
            _disposable?.Dispose();
        }

        internal void SetTransform(SKMatrix skMatrix)
        {
            var skMatrixTotal = _skCanvas.TotalMatrix;
            SKMatrix.PreConcat(ref skMatrixTotal, ref skMatrix);
            _skCanvas.SetMatrix(skMatrixTotal);
        }

        internal void SetClipPath(SvgVisualElement svgVisualElement, CompositeDisposable disposable)
        {
            var skPathClip = SkiaUtil.GetSvgVisualElementClipPath(svgVisualElement, disposable);
            if (skPathClip != null && !skPathClip.IsEmpty)
            {
                bool antialias = SkiaUtil.IsAntialias(svgVisualElement);
                _skCanvas.ClipPath(skPathClip, SKClipOperation.Intersect, antialias);
            }
        }

        internal void SetClip(SvgVisualElement svgVisualElement, SKRect sKRectBounds)
        {
            var clip = svgVisualElement.Clip;
            if (!string.IsNullOrEmpty(clip) && clip.StartsWith("rect("))
            {
                clip = clip.Trim();
                var offsets = (from o in clip.Substring(5, clip.Length - 6).Split(',')
                               select float.Parse(o.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture)).ToList();
                var clipRect = SKRect.Create(
                    sKRectBounds.Left + offsets[3],
                    sKRectBounds.Top + offsets[0],
                    sKRectBounds.Width - (offsets[3] + offsets[1]),
                    sKRectBounds.Height - (offsets[2] + offsets[0]));
                _skCanvas.ClipRect(clipRect, SKClipOperation.Intersect);
            }
        }

        internal SKPaint? SetOpacity(SvgElement svgElement, CompositeDisposable disposable)
        {
            float opacity = SkiaUtil.AdjustSvgOpacity(svgElement.Opacity);
            if (opacity < 1f)
            {
                var skPaint = new SKPaint()
                {
                    IsAntialias = true,
                };
                skPaint.Color = new SKColor(255, 255, 255, (byte)Math.Round(opacity * 255));
                skPaint.Style = SKPaintStyle.StrokeAndFill;
                _skCanvas.SaveLayer(skPaint);
                disposable.Add(skPaint);
                return skPaint;
            }
            return null;
        }

        internal SKPaint? SetFilter(SvgVisualElement svgVisualElement, CompositeDisposable disposable)
        {
            if (svgVisualElement.Filter != null)
            {
                var skPaint = new SKPaint();
                skPaint.Style = SKPaintStyle.StrokeAndFill;
                SkiaUtil.SetFilter(svgVisualElement, skPaint, disposable);
                _skCanvas.SaveLayer(skPaint);
                disposable.Add(skPaint);
                return skPaint;
            }
            return null;
        }

        internal void Draw(SvgElement svgElement, bool ignoreDisplay)
        {
            switch (svgElement)
            {
                // TODO:
                //case SvgAnchor svgAnchor:
                //    DrawAnchor(svgAnchor, ignoreDisplay);
                //    break;
                case SvgFragment svgFragment:
                    DrawFragment(svgFragment, ignoreDisplay);
                    break;
                case SvgImage svgImage:
                    DrawImage(svgImage, ignoreDisplay);
                    break;
                case SvgSwitch svgSwitch:
                    DrawSwitch(svgSwitch, ignoreDisplay);
                    break;
                case SvgUse svgUse:
                    DrawUse(svgUse, ignoreDisplay);
                    break;
                case SvgForeignObject svgForeignObject:
                    DrawForeignObject(svgForeignObject, ignoreDisplay);
                    break;
                case SvgCircle svgCircle:
                    DrawCircle(svgCircle, ignoreDisplay);
                    break;
                case SvgEllipse svgEllipse:
                    DrawEllipse(svgEllipse, ignoreDisplay);
                    break;
                case SvgRectangle svgRectangle:
                    DrawRectangle(svgRectangle, ignoreDisplay);
                    break;
                case SvgGlyph svgGlyph:
                    DrawGlyph(svgGlyph, ignoreDisplay);
                    break;
                case SvgGroup svgGroup:
                    DrawGroup(svgGroup, ignoreDisplay);
                    break;
                case SvgLine svgLine:
                    DrawLine(svgLine, ignoreDisplay);
                    break;
                case SvgPath svgPath:
                    DrawPath(svgPath, ignoreDisplay);
                    break;
                case SvgPolyline svgPolyline:
                    DrawPolyline(svgPolyline, ignoreDisplay);
                    break;
                case SvgPolygon svgPolygon:
                    DrawPolygon(svgPolygon, ignoreDisplay);
                    break;
                case SvgText svgText:
                    DrawText(svgText, ignoreDisplay);
                    break;
                case SvgTextPath svgTextPath:
                    DrawTextPath(svgTextPath, ignoreDisplay);
                    break;
                case SvgTextRef svgTextRef:
                    DrawTextRef(svgTextRef, ignoreDisplay);
                    break;
                case SvgTextSpan svgTextSpan:
                    DrawTextSpan(svgTextSpan, ignoreDisplay);
                    break;
                default:
                    break;
            }
        }

        internal SvgVisualElement? GetMarkerElement(SvgMarker svgMarker)
        {
            SvgVisualElement? markerElement = null;

            foreach (var child in svgMarker.Children)
            {
                if (child is SvgVisualElement svgVisualElement)
                {
                    markerElement = svgVisualElement;
                    break;
                }
            }

            return markerElement;
        }

        internal void DrawMarker(SvgMarker svgMarker, SvgVisualElement pOwner, SKPoint pMarkerPoint, float fAngle)
        {
            var markerElement = GetMarkerElement(svgMarker);
            if (markerElement == null)
            {
                return;
            }

            var skMarkerMatrix = SKMatrix.MakeIdentity();

            var skMatrixMarkerPoint = SKMatrix.MakeTranslation(pMarkerPoint.X, pMarkerPoint.Y);
            SKMatrix.PreConcat(ref skMarkerMatrix, ref skMatrixMarkerPoint);

            var skMatrixAngle = SKMatrix.MakeRotationDegrees(svgMarker.Orient.IsAuto ? fAngle : svgMarker.Orient.Angle);
            SKMatrix.PreConcat(ref skMarkerMatrix, ref skMatrixAngle);

            var strokeWidth = pOwner.StrokeWidth.ToDeviceValue(null, UnitRenderingType.Other, svgMarker);

            var refX = svgMarker.RefX.ToDeviceValue(null, UnitRenderingType.Horizontal, svgMarker);
            var refY = svgMarker.RefY.ToDeviceValue(null, UnitRenderingType.Horizontal, svgMarker);
            float markerWidth = svgMarker.MarkerWidth;
            float markerHeight = svgMarker.MarkerHeight;
            float viewBoxToMarkerUnitsScaleX = 1f;
            float viewBoxToMarkerUnitsScaleY = 1f;

            switch (svgMarker.MarkerUnits)
            {
                case SvgMarkerUnits.StrokeWidth:
                    {
                        var skMatrixStrokeWidth = SKMatrix.MakeScale(strokeWidth, strokeWidth);
                        SKMatrix.PreConcat(ref skMarkerMatrix, ref skMatrixStrokeWidth);

                        var viewBoxWidth = svgMarker.ViewBox.Width;
                        var viewBoxHeight = svgMarker.ViewBox.Height;

                        var scaleFactorWidth = (viewBoxWidth <= 0) ? 1 : (markerWidth / viewBoxWidth);
                        var scaleFactorHeight = (viewBoxHeight <= 0) ? 1 : (markerHeight / viewBoxHeight);

                        viewBoxToMarkerUnitsScaleX = Math.Min(scaleFactorWidth, scaleFactorHeight);
                        viewBoxToMarkerUnitsScaleY = Math.Min(scaleFactorWidth, scaleFactorHeight);

                        var skMatrixTranslateRefXY = SKMatrix.MakeTranslation(-refX * viewBoxToMarkerUnitsScaleX, -refY * viewBoxToMarkerUnitsScaleY);
                        SKMatrix.PreConcat(ref skMarkerMatrix, ref skMatrixTranslateRefXY);

                        var skMatrixScaleXY = SKMatrix.MakeScale(viewBoxToMarkerUnitsScaleX, viewBoxToMarkerUnitsScaleY);
                        SKMatrix.PreConcat(ref skMarkerMatrix, ref skMatrixScaleXY);
                    }
                    break;
                case SvgMarkerUnits.UserSpaceOnUse:
                    {
                        var skMatrixTranslateRefXY = SKMatrix.MakeTranslation(-refX, -refY);
                        SKMatrix.PreConcat(ref skMarkerMatrix, ref skMatrixTranslateRefXY);
                    }
                    break;
            }

            _skCanvas.Save();

            var skMatrix = SkiaUtil.GetSKMatrix(svgMarker.Transforms);
            SKMatrix.PreConcat(ref skMatrix, ref skMarkerMatrix);
            SetTransform(skMatrix);
            SetClipPath(svgMarker, _disposable);

            var skPaintOpacity = SetOpacity(svgMarker, _disposable);

            var skPaintFilter = SetFilter(svgMarker, _disposable);

            switch (svgMarker.Overflow)
            {
                case SvgOverflow.Auto:
                case SvgOverflow.Visible:
                case SvgOverflow.Inherit:
                    break;
                default:
                    var skClipRect = SKRect.Create(
                        svgMarker.ViewBox.MinX,
                        svgMarker.ViewBox.MinY,
                        markerWidth / viewBoxToMarkerUnitsScaleX,
                        markerHeight / viewBoxToMarkerUnitsScaleY);
                    _skCanvas.ClipRect(skClipRect, SKClipOperation.Intersect);
                    break;
            }

            Draw(markerElement, true);

            if (skPaintFilter != null)
            {
                _skCanvas.Restore();
            }

            if (skPaintOpacity != null)
            {
                _skCanvas.Restore();
            }

            _skCanvas.Restore();
        }

        internal void DrawMarker(SvgMarker svgMarker, SvgVisualElement pOwner, SKPoint pRefPoint, SKPoint pMarkerPoint1, SKPoint pMarkerPoint2, bool isStartMarker)
        {
            float fAngle1 = 0f;
            if (svgMarker.Orient.IsAuto)
            {
                float xDiff = pMarkerPoint2.X - pMarkerPoint1.X;
                float yDiff = pMarkerPoint2.Y - pMarkerPoint1.Y;
                fAngle1 = (float)(Math.Atan2(yDiff, xDiff) * 180.0 / Math.PI);
                if (isStartMarker && svgMarker.Orient.IsAutoStartReverse)
                {
                    fAngle1 += 180;
                }
            }
            DrawMarker(svgMarker, pOwner, pRefPoint, fAngle1);
        }

        internal void DrawMarker(SvgMarker svgMarker, SvgVisualElement pOwner, SKPoint pRefPoint, SKPoint pMarkerPoint1, SKPoint pMarkerPoint2, SKPoint pMarkerPoint3)
        {
            float xDiff = pMarkerPoint2.X - pMarkerPoint1.X;
            float yDiff = pMarkerPoint2.Y - pMarkerPoint1.Y;
            float fAngle1 = (float)(Math.Atan2(yDiff, xDiff) * 180.0 / Math.PI);
            xDiff = pMarkerPoint3.X - pMarkerPoint2.X;
            yDiff = pMarkerPoint3.Y - pMarkerPoint2.Y;
            float fAngle2 = (float)(Math.Atan2(yDiff, xDiff) * 180.0 / Math.PI);
            DrawMarker(svgMarker, pOwner, pRefPoint, (fAngle1 + fAngle2) / 2);
        }

        internal void DrawMarkers(SvgMarkerElement svgMarkerElement, SKPath sKPath)
        {
            var pathTypes = SkiaUtil.GetPathTypes(sKPath);
            var pathLength = pathTypes.Count;

            if (svgMarkerElement.MarkerStart != null)
            {
                var refPoint1 = pathTypes[0].Point;
                var index = 1;
                while (index < pathLength && pathTypes[index].Point == refPoint1)
                {
                    ++index;
                }
                var refPoint2 = pathTypes[index].Point;
                var marker = svgMarkerElement.OwnerDocument.GetElementById<SvgMarker>(svgMarkerElement.MarkerStart.ToString());
                DrawMarker(marker, svgMarkerElement, refPoint1, refPoint1, refPoint2, true);
            }

            if (svgMarkerElement.MarkerMid != null)
            {
                var marker = svgMarkerElement.OwnerDocument.GetElementById<SvgMarker>(svgMarkerElement.MarkerMid.ToString());
                int bezierIndex = -1;
                for (int i = 1; i <= pathLength - 2; i++)
                {
                    // for Bezier curves, the marker shall only been shown at the last point
                    if ((pathTypes[i].Type & (byte)PathPointType.PathTypeMask) == (byte)PathPointType.Bezier)
                        bezierIndex = (bezierIndex + 1) % 3;
                    else
                        bezierIndex = -1;

                    if (bezierIndex == -1 || bezierIndex == 2)
                    {
                        DrawMarker(marker, svgMarkerElement, pathTypes[i].Point, pathTypes[i - 1].Point, pathTypes[i].Point, pathTypes[i + 1].Point);
                    }
                }
            }

            if (svgMarkerElement.MarkerEnd != null)
            {
                var marker = svgMarkerElement.OwnerDocument.GetElementById<SvgMarker>(svgMarkerElement.MarkerEnd.ToString());
                var index = pathLength - 1;
                var refPoint1 = pathTypes[index].Point;
                --index;
                while (index > 0 && pathTypes[index].Point == refPoint1)
                {
                    --index;
                }
                var refPoint2 = pathTypes[index].Point;
                DrawMarker(marker, svgMarkerElement, refPoint1, refPoint2, pathTypes[pathLength - 1].Point, false);
            }
        }

        internal void AddMarkers(SvgGroup svgGroup)
        {
            Uri? marker = null;
            // TODO: marker can not be set as presentation attribute
            //if (svgGroup.TryGetAttribute("marker", out string markerUrl))
            //{
            //    marker = new Uri(markerUrl, UriKind.RelativeOrAbsolute);
            //}

            if (svgGroup.MarkerStart == null && svgGroup.MarkerMid == null && svgGroup.MarkerEnd == null && marker == null)
            {
                return;
            }

            foreach (var svgElement in svgGroup.Children)
            {
                if (svgElement is SvgMarkerElement svgMarkerElement)
                {
                    if (svgMarkerElement.MarkerStart == null)
                    {
                        if (svgGroup.MarkerStart != null)
                        {
                            svgMarkerElement.MarkerStart = svgGroup.MarkerStart;
                        }
                        else if (marker != null)
                        {
                            svgMarkerElement.MarkerStart = marker;
                        }
                    }
                    if (svgMarkerElement.MarkerMid == null)
                    {
                        if (svgGroup.MarkerMid != null)
                        {
                            svgMarkerElement.MarkerMid = svgGroup.MarkerMid;
                        }
                        else if (marker != null)
                        {
                            svgMarkerElement.MarkerMid = marker;
                        }
                    }
                    if (svgMarkerElement.MarkerEnd == null)
                    {
                        if (svgGroup.MarkerEnd != null)
                        {
                            svgMarkerElement.MarkerEnd = svgGroup.MarkerEnd;
                        }
                        else if (marker != null)
                        {
                            svgMarkerElement.MarkerEnd = marker;
                        }
                    }
                }
            }
        }

        internal bool CanDraw(SvgVisualElement svgVisualElement, bool ignoreDisplay)
        {
            bool visible = svgVisualElement.Visible == true;
            bool display = ignoreDisplay ? true : !string.Equals(svgVisualElement.Display, "none", StringComparison.OrdinalIgnoreCase);
            return visible && display;
        }

        // TODO:
        //public void DrawAnchor(SvgAnchor svgAnchor, bool ignoreDisplay)
        //{
        //    _skCanvas.Save();
        //
        //    var skMatrix = SkiaUtil.GetSKMatrix(svgAnchor.Transforms);
        //    SetTransform(skMatrix);
        //
        //    var skPaintOpacity = SetOpacity(svgAnchor, _disposable);
        //
        //    foreach (var svgElement in svgAnchor.Children)
        //    {
        //        Draw(svgElement, ignoreDisplay);
        //    }
        //
        //    if (skPaintOpacity != null)
        //    {
        //        _skCanvas.Restore();
        //    }
        //
        //    _skCanvas.Restore();
        //}

        public void DrawFragment(SvgFragment svgFragment, bool ignoreDisplay)
        {
            float x = svgFragment.X.ToDeviceValue(null, UnitRenderingType.Horizontal, svgFragment);
            float y = svgFragment.Y.ToDeviceValue(null, UnitRenderingType.Vertical, svgFragment);
            var skSize = SkiaUtil.GetDimensions(svgFragment);

            _skCanvas.Save();

            switch (svgFragment.Overflow)
            {
                case SvgOverflow.Auto:
                case SvgOverflow.Visible:
                case SvgOverflow.Inherit:
                    break;
                default:
                    var skClipRect = SKRect.Create(x, y, skSize.Width, skSize.Height);
                    _skCanvas.ClipRect(skClipRect, SKClipOperation.Intersect);
                    break;
            }

            var skMatrixViewBox = SkiaUtil.GetSvgViewBoxTransform(svgFragment.ViewBox, svgFragment.AspectRatio, x, y, skSize.Width, skSize.Height);
            var skMatrix = SkiaUtil.GetSKMatrix(svgFragment.Transforms);
            SKMatrix.PreConcat(ref skMatrix, ref skMatrixViewBox);
            SetTransform(skMatrix);

            var skPaintOpacity = SetOpacity(svgFragment, _disposable);

            foreach (var svgElement in svgFragment.Children)
            {
                Draw(svgElement, ignoreDisplay);
            }

            if (skPaintOpacity != null)
            {
                _skCanvas.Restore();
            }

            _skCanvas.Restore();
        }

        public void DrawImage(SvgImage svgImage, bool ignoreDisplay)
        {
            if (!CanDraw(svgImage, ignoreDisplay))
            {
                return;
            }

            float width = svgImage.Width.ToDeviceValue(null, UnitRenderingType.Horizontal, svgImage);
            float height = svgImage.Height.ToDeviceValue(null, UnitRenderingType.Vertical, svgImage);
            var location = svgImage.Location.ToDeviceValue(null, svgImage);

            if (width <= 0f || height <= 0f || svgImage.Href == null)
            {
                return;
            }

            var image = SkiaUtil.GetImage(svgImage, svgImage.Href);
            var skImage = image as SKImage;
            var svgFragment = image as SvgFragment;
            if (skImage == null && svgFragment == null)
            {
                return;
            }

            if (skImage != null)
            {
                _disposable.Add(skImage);
            }

            SKRect srcRect = default;

            if (skImage != null)
            {
                srcRect = SKRect.Create(0f, 0f, skImage.Width, skImage.Height);
            }

            if (svgFragment != null)
            {
                var skSize = SkiaUtil.GetDimensions(svgFragment);
                srcRect = SKRect.Create(0f, 0f, skSize.Width, skSize.Height);
            }

            var destClip = SKRect.Create(location.X, location.Y, width, height);
            var destRect = destClip;

            var aspectRatio = svgImage.AspectRatio;
            if (aspectRatio.Align != SvgPreserveAspectRatio.none)
            {
                var fScaleX = destClip.Width / srcRect.Width;
                var fScaleY = destClip.Height / srcRect.Height;
                var xOffset = 0f;
                var yOffset = 0f;

                if (aspectRatio.Slice)
                {
                    fScaleX = Math.Max(fScaleX, fScaleY);
                    fScaleY = Math.Max(fScaleX, fScaleY);
                }
                else
                {
                    fScaleX = Math.Min(fScaleX, fScaleY);
                    fScaleY = Math.Min(fScaleX, fScaleY);
                }

                switch (aspectRatio.Align)
                {
                    case SvgPreserveAspectRatio.xMinYMin:
                        break;
                    case SvgPreserveAspectRatio.xMidYMin:
                        xOffset = (destClip.Width - srcRect.Width * fScaleX) / 2;
                        break;
                    case SvgPreserveAspectRatio.xMaxYMin:
                        xOffset = (destClip.Width - srcRect.Width * fScaleX);
                        break;
                    case SvgPreserveAspectRatio.xMinYMid:
                        yOffset = (destClip.Height - srcRect.Height * fScaleY) / 2;
                        break;
                    case SvgPreserveAspectRatio.xMidYMid:
                        xOffset = (destClip.Width - srcRect.Width * fScaleX) / 2;
                        yOffset = (destClip.Height - srcRect.Height * fScaleY) / 2;
                        break;
                    case SvgPreserveAspectRatio.xMaxYMid:
                        xOffset = (destClip.Width - srcRect.Width * fScaleX);
                        yOffset = (destClip.Height - srcRect.Height * fScaleY) / 2;
                        break;
                    case SvgPreserveAspectRatio.xMinYMax:
                        yOffset = (destClip.Height - srcRect.Height * fScaleY);
                        break;
                    case SvgPreserveAspectRatio.xMidYMax:
                        xOffset = (destClip.Width - srcRect.Width * fScaleX) / 2;
                        yOffset = (destClip.Height - srcRect.Height * fScaleY);
                        break;
                    case SvgPreserveAspectRatio.xMaxYMax:
                        xOffset = (destClip.Width - srcRect.Width * fScaleX);
                        yOffset = (destClip.Height - srcRect.Height * fScaleY);
                        break;
                }

                destRect = SKRect.Create(
                    destClip.Left + xOffset, destClip.Top + yOffset,
                    srcRect.Width * fScaleX, srcRect.Height * fScaleY);
            }

            _skCanvas.Save();

            var skMatrix = SkiaUtil.GetSKMatrix(svgImage.Transforms);
            SetTransform(skMatrix);
            SetClipPath(svgImage, _disposable);

            var skPaintOpacity = SetOpacity(svgImage, _disposable);

            var skPaintFilter = SetFilter(svgImage, _disposable);

            _skCanvas.ClipRect(destClip, SKClipOperation.Intersect);

            SetClip(svgImage, destClip);

            if (skImage != null)
            {
                _skCanvas.DrawImage(skImage, srcRect, destRect);
            }

            if (svgFragment != null)
            {
                _skCanvas.Save();

                float dx = destRect.Left;
                float dy = destRect.Top;
                float sx = destRect.Width / srcRect.Width;
                float sy = destRect.Height / srcRect.Height;
                var skTranslationMatrix = SKMatrix.MakeTranslation(dx, dy);
                var skScaleMatrix = SKMatrix.MakeScale(sx, sy);
                SKMatrix.PreConcat(ref skTranslationMatrix, ref skScaleMatrix);
                SetTransform(skTranslationMatrix);

                DrawFragment(svgFragment, ignoreDisplay);

                _skCanvas.Restore();
            }

            if (skPaintFilter != null)
            {
                _skCanvas.Restore();
            }

            if (skPaintOpacity != null)
            {
                _skCanvas.Restore();
            }

            _skCanvas.Restore();
        }

        public void DrawSwitch(SvgSwitch svgSwitch, bool ignoreDisplay)
        {
            if (!CanDraw(svgSwitch, ignoreDisplay))
            {
                return;
            }

            _skCanvas.Save();

            var skMatrix = SkiaUtil.GetSKMatrix(svgSwitch.Transforms);
            SetTransform(skMatrix);
            SetClipPath(svgSwitch, _disposable);

            var skPaintOpacity = SetOpacity(svgSwitch, _disposable);

            var skPaintFilter = SetFilter(svgSwitch, _disposable);

            // TODO:

            if (skPaintFilter != null)
            {
                _skCanvas.Restore();
            }

            if (skPaintOpacity != null)
            {
                _skCanvas.Restore();
            }

            _skCanvas.Restore();
        }

        public void DrawSymbol(SvgSymbol svgSymbol, bool ignoreDisplay)
        {
            if (!CanDraw(svgSymbol, ignoreDisplay))
            {
                return;
            }

            _skCanvas.Save();

            float x = 0f;
            float y = 0f;
            float width = svgSymbol.ViewBox.Width;
            float height = svgSymbol.ViewBox.Height;

            if (svgSymbol.CustomAttributes.TryGetValue("width", out string? _widthString))
            {
                if (new SvgUnitConverter().ConvertFrom(_widthString) is SvgUnit _width)
                {
                    width = _width.ToDeviceValue(null, UnitRenderingType.Horizontal, svgSymbol);
                }
            }

            if (svgSymbol.CustomAttributes.TryGetValue("height", out string? heightString))
            {
                if (new SvgUnitConverter().ConvertFrom(heightString) is SvgUnit _height)
                {
                    height = _height.ToDeviceValue(null, UnitRenderingType.Vertical, svgSymbol);
                }
            }

            var skRectBounds = SKRect.Create(x, y, width, height);

            var skMatrixViewBox = SkiaUtil.GetSvgViewBoxTransform(svgSymbol.ViewBox, svgSymbol.AspectRatio, x, y, width, height);
            var skMatrix = SkiaUtil.GetSKMatrix(svgSymbol.Transforms);
            SKMatrix.PreConcat(ref skMatrix, ref skMatrixViewBox);
            SetTransform(skMatrix);
            SetClipPath(svgSymbol, _disposable);

            var skPaintOpacity = SetOpacity(svgSymbol, _disposable);

            var skPaintFilter = SetFilter(svgSymbol, _disposable);

            foreach (var svgElement in svgSymbol.Children)
            {
                Draw(svgElement, ignoreDisplay);
            }

            if (skPaintFilter != null)
            {
                _skCanvas.Restore();
            }

            if (skPaintOpacity != null)
            {
                _skCanvas.Restore();
            }

            _skCanvas.Restore();
        }

        public void DrawUse(SvgUse svgUse, bool ignoreDisplay)
        {
            if (!CanDraw(svgUse, ignoreDisplay))
            {
                return;
            }

            var svgVisualElement = SkiaUtil.GetReference<SvgVisualElement>(svgUse, svgUse.ReferencedElement);
            if (svgVisualElement == null || SkiaUtil.HasRecursiveReference(svgUse))
            {
                return;
            }

            float x = svgUse.X.ToDeviceValue(null, UnitRenderingType.Horizontal, svgUse);
            float y = svgUse.Y.ToDeviceValue(null, UnitRenderingType.Vertical, svgUse);
            var skMatrixTranslateXY = SKMatrix.MakeTranslation(x, y);
            var skMatrix = SkiaUtil.GetSKMatrix(svgUse.Transforms);
            SKMatrix.PreConcat(ref skMatrix, ref skMatrixTranslateXY);

            var ew = svgUse.Width.ToDeviceValue(null, UnitRenderingType.Horizontal, svgUse);
            var eh = svgUse.Height.ToDeviceValue(null, UnitRenderingType.Vertical, svgUse);
            if (ew > 0 && eh > 0)
            {
                var _attributes = svgVisualElement.GetType().GetField("_attributes", BindingFlags.NonPublic | BindingFlags.Instance);
                if (_attributes != null)
                {
                    var attributes = _attributes.GetValue(svgVisualElement) as SvgAttributeCollection;
                    if (attributes != null)
                    {
                        var viewBox = attributes.GetAttribute<SvgViewBox>("viewBox");
                        if (viewBox != SvgViewBox.Empty && Math.Abs(ew - viewBox.Width) > float.Epsilon && Math.Abs(eh - viewBox.Height) > float.Epsilon)
                        {
                            var sw = ew / viewBox.Width;
                            var sh = eh / viewBox.Height;

                            var skMatrixTranslateSWSH = SKMatrix.MakeTranslation(sw, sh);
                            SKMatrix.PreConcat(ref skMatrix, ref skMatrixTranslateSWSH);
                        }
                    }
                }
            }

            var originalParent = svgUse.Parent;
            var useParent = svgUse.GetType().GetField("_parent", BindingFlags.NonPublic | BindingFlags.Instance);
            if (useParent != null)
            {
                useParent.SetValue(svgVisualElement, svgUse);
            }

            svgVisualElement.InvalidateChildPaths();

            _skCanvas.Save();

            SetTransform(skMatrix);
            SetClipPath(svgUse, _disposable);

            var skPaintOpacity = SetOpacity(svgUse, _disposable);

            var skPaintFilter = SetFilter(svgUse, _disposable);

            if (svgVisualElement is SvgSymbol svgSymbol)
            {
                DrawSymbol(svgSymbol, ignoreDisplay);
            }
            else
            {
                Draw(svgVisualElement, ignoreDisplay);
            }

            if (skPaintFilter != null)
            {
                _skCanvas.Restore();
            }

            if (skPaintOpacity != null)
            {
                _skCanvas.Restore();
            }

            _skCanvas.Restore();

            if (useParent != null)
            {
                useParent.SetValue(svgVisualElement, originalParent);
            }
        }

        public void DrawForeignObject(SvgForeignObject svgForeignObject, bool ignoreDisplay)
        {
            if (!CanDraw(svgForeignObject, ignoreDisplay))
            {
                return;
            }

            _skCanvas.Save();

            var skMatrix = SkiaUtil.GetSKMatrix(svgForeignObject.Transforms);
            SetTransform(skMatrix);
            SetClipPath(svgForeignObject, _disposable);

            var skPaintOpacity = SetOpacity(svgForeignObject, _disposable);

            var skPaintFilter = SetFilter(svgForeignObject, _disposable);

            // TODO:

            if (skPaintFilter != null)
            {
                _skCanvas.Restore();
            }

            if (skPaintOpacity != null)
            {
                _skCanvas.Restore();
            }

            _skCanvas.Restore();
        }

        public void DrawCircle(SvgCircle svgCircle, bool ignoreDisplay)
        {
            if (!CanDraw(svgCircle, ignoreDisplay))
            {
                return;
            }

            float cx = svgCircle.CenterX.ToDeviceValue(null, UnitRenderingType.Horizontal, svgCircle);
            float cy = svgCircle.CenterY.ToDeviceValue(null, UnitRenderingType.Vertical, svgCircle);
            float radius = svgCircle.Radius.ToDeviceValue(null, UnitRenderingType.Other, svgCircle);

            if (radius <= 0f)
            {
                return;
            }

            var skRectBounds = SKRect.Create(cx - radius, cy - radius, radius + radius, radius + radius);

            _skCanvas.Save();

            var skMatrix = SkiaUtil.GetSKMatrix(svgCircle.Transforms);
            SetTransform(skMatrix);
            SetClipPath(svgCircle, _disposable);

            var skPaintOpacity = SetOpacity(svgCircle, _disposable);

            var skPaintFilter = SetFilter(svgCircle, _disposable);

            if (SkiaUtil.IsValidFill(svgCircle))
            {
                var skPaintFill = SkiaUtil.GetFillSKPaint(svgCircle, _skSize, skRectBounds, _disposable);
                _skCanvas.DrawCircle(cx, cy, radius, skPaintFill);
            }

            if (SkiaUtil.IsValidStroke(svgCircle))
            {
                var skPaintStroke = SkiaUtil.GetStrokeSKPaint(svgCircle, _skSize, skRectBounds, _disposable);
                _skCanvas.DrawCircle(cx, cy, radius, skPaintStroke);
            }

            if (skPaintFilter != null)
            {
                _skCanvas.Restore();
            }

            if (skPaintOpacity != null)
            {
                _skCanvas.Restore();
            }

            _skCanvas.Restore();
        }

        public void DrawEllipse(SvgEllipse svgEllipse, bool ignoreDisplay)
        {
            if (!CanDraw(svgEllipse, ignoreDisplay))
            {
                return;
            }

            float cx = svgEllipse.CenterX.ToDeviceValue(null, UnitRenderingType.Horizontal, svgEllipse);
            float cy = svgEllipse.CenterY.ToDeviceValue(null, UnitRenderingType.Vertical, svgEllipse);
            float rx = svgEllipse.RadiusX.ToDeviceValue(null, UnitRenderingType.Other, svgEllipse);
            float ry = svgEllipse.RadiusY.ToDeviceValue(null, UnitRenderingType.Other, svgEllipse);

            if (rx <= 0f || ry <= 0f)
            {
                return;
            }

            var skRectBounds = SKRect.Create(cx - rx, cy - ry, rx + rx, ry + ry);

            _skCanvas.Save();

            var skMatrix = SkiaUtil.GetSKMatrix(svgEllipse.Transforms);
            SetTransform(skMatrix);
            SetClipPath(svgEllipse, _disposable);

            var skPaintOpacity = SetOpacity(svgEllipse, _disposable);

            var skPaintFilter = SetFilter(svgEllipse, _disposable);

            if (SkiaUtil.IsValidFill(svgEllipse))
            {
                var skPaintFill = SkiaUtil.GetFillSKPaint(svgEllipse, _skSize, skRectBounds, _disposable);
                _skCanvas.DrawOval(cx, cy, rx, ry, skPaintFill);
            }

            if (SkiaUtil.IsValidStroke(svgEllipse))
            {
                var skPaintStroke = SkiaUtil.GetStrokeSKPaint(svgEllipse, _skSize, skRectBounds, _disposable);
                _skCanvas.DrawOval(cx, cy, rx, ry, skPaintStroke);
            }

            if (skPaintFilter != null)
            {
                _skCanvas.Restore();
            }

            if (skPaintOpacity != null)
            {
                _skCanvas.Restore();
            }

            _skCanvas.Restore();
        }

        public void DrawRectangle(SvgRectangle svgRectangle, bool ignoreDisplay)
        {
            if (!CanDraw(svgRectangle, ignoreDisplay))
            {
                return;
            }

            float x = svgRectangle.X.ToDeviceValue(null, UnitRenderingType.Horizontal, svgRectangle);
            float y = svgRectangle.Y.ToDeviceValue(null, UnitRenderingType.Vertical, svgRectangle);
            float width = svgRectangle.Width.ToDeviceValue(null, UnitRenderingType.Horizontal, svgRectangle);
            float height = svgRectangle.Height.ToDeviceValue(null, UnitRenderingType.Vertical, svgRectangle);
            float rx = svgRectangle.CornerRadiusX.ToDeviceValue(null, UnitRenderingType.Horizontal, svgRectangle);
            float ry = svgRectangle.CornerRadiusY.ToDeviceValue(null, UnitRenderingType.Vertical, svgRectangle);

            if (width <= 0f || height <= 0f || rx < 0f || ry < 0f)
            {
                return;
            }

            if (rx > 0f)
            {
                float halfWidth = width / 2f;
                if (rx > halfWidth)
                {
                    rx = halfWidth;
                }
            }

            if (ry > 0f)
            {
                float halfHeight = height / 2f;
                if (ry > halfHeight)
                {
                    ry = halfHeight;
                }
            }

            bool isRound = rx > 0f && ry > 0f;
            var skRectBounds = SKRect.Create(x, y, width, height);

            _skCanvas.Save();

            var skMatrix = SkiaUtil.GetSKMatrix(svgRectangle.Transforms);
            SetTransform(skMatrix);
            SetClipPath(svgRectangle, _disposable);

            var skPaintOpacity = SetOpacity(svgRectangle, _disposable);

            var skPaintFilter = SetFilter(svgRectangle, _disposable);

            if (SkiaUtil.IsValidFill(svgRectangle))
            {
                var skPaintFill = SkiaUtil.GetFillSKPaint(svgRectangle, _skSize, skRectBounds, _disposable);
                if (isRound)
                {
                    _skCanvas.DrawRoundRect(x, y, width, height, rx, ry, skPaintFill);
                }
                else
                {
                    _skCanvas.DrawRect(x, y, width, height, skPaintFill);
                }
            }

            if (SkiaUtil.IsValidStroke(svgRectangle))
            {
                var skPaintStroke = SkiaUtil.GetStrokeSKPaint(svgRectangle, _skSize, skRectBounds, _disposable);
                if (isRound)
                {
                    _skCanvas.DrawRoundRect(skRectBounds, rx, ry, skPaintStroke);
                }
                else
                {
                    _skCanvas.DrawRect(skRectBounds, skPaintStroke);
                }
            }

            if (skPaintFilter != null)
            {
                _skCanvas.Restore();
            }

            if (skPaintOpacity != null)
            {
                _skCanvas.Restore();
            }

            _skCanvas.Restore();
        }

        public void DrawGlyph(SvgGlyph svgGlyph, bool ignoreDisplay)
        {
            if (!CanDraw(svgGlyph, ignoreDisplay))
            {
                return;
            }

            _skCanvas.Save();

            var skMatrix = SkiaUtil.GetSKMatrix(svgGlyph.Transforms);
            SetTransform(skMatrix);
            SetClipPath(svgGlyph, _disposable);

            var skPaintOpacity = SetOpacity(svgGlyph, _disposable);

            var skPaintFilter = SetFilter(svgGlyph, _disposable);

            // TODO:

            if (skPaintFilter != null)
            {
                _skCanvas.Restore();
            }

            if (skPaintOpacity != null)
            {
                _skCanvas.Restore();
            }

            _skCanvas.Restore();
        }

        public void DrawGroup(SvgGroup svgGroup, bool ignoreDisplay)
        {
            if (!CanDraw(svgGroup, ignoreDisplay))
            {
                return;
            }

            // TODO: Call AddMarkers only once.
            AddMarkers(svgGroup);

            _skCanvas.Save();

            var skMatrix = SkiaUtil.GetSKMatrix(svgGroup.Transforms);
            SetTransform(skMatrix);
            SetClipPath(svgGroup, _disposable);

            var skPaintOpacity = SetOpacity(svgGroup, _disposable);

            var skPaintFilter = SetFilter(svgGroup, _disposable);

            foreach (var svgElement in svgGroup.Children)
            {
                Draw(svgElement, ignoreDisplay);
            }

            if (skPaintFilter != null)
            {
                _skCanvas.Restore();
            }

            if (skPaintOpacity != null)
            {
                _skCanvas.Restore();
            }

            _skCanvas.Restore();
        }

        public void DrawLine(SvgLine svgLine, bool ignoreDisplay)
        {
            if (!CanDraw(svgLine, ignoreDisplay))
            {
                return;
            }

            _skCanvas.Save();

            var skMatrix = SkiaUtil.GetSKMatrix(svgLine.Transforms);
            SetTransform(skMatrix);
            SetClipPath(svgLine, _disposable);

            var skPaintOpacity = SetOpacity(svgLine, _disposable);

            var skPaintFilter = SetFilter(svgLine, _disposable);

            var skPath = SkiaUtil.ToSKPath(svgLine, svgLine.FillRule, _disposable);
            if (skPath != null && !skPath.IsEmpty)
            {
                var skBounds = skPath.Bounds;

                if (SkiaUtil.IsValidStroke(svgLine))
                {
                    var skPaint = SkiaUtil.GetStrokeSKPaint(svgLine, _skSize, skBounds, _disposable);
                    _skCanvas.DrawPath(skPath, skPaint);
                }

                DrawMarkers(svgLine, skPath);
            }

            if (skPaintFilter != null)
            {
                _skCanvas.Restore();
            }

            if (skPaintOpacity != null)
            {
                _skCanvas.Restore();
            }

            _skCanvas.Restore();
        }

        public void DrawPath(SvgPath svgPath, bool ignoreDisplay)
        {
            if (!CanDraw(svgPath, ignoreDisplay))
            {
                return;
            }

            _skCanvas.Save();

            var skMatrix = SkiaUtil.GetSKMatrix(svgPath.Transforms);
            SetTransform(skMatrix);
            SetClipPath(svgPath, _disposable);

            var skPaintOpacity = SetOpacity(svgPath, _disposable);

            var skPaintFilter = SetFilter(svgPath, _disposable);

            var skPath = SkiaUtil.ToSKPath(svgPath.PathData, svgPath.FillRule, _disposable);
            if (skPath != null && !skPath.IsEmpty)
            {
                var skBounds = skPath.Bounds;

                if (SkiaUtil.IsValidFill(svgPath))
                {
                    var skPaint = SkiaUtil.GetFillSKPaint(svgPath, _skSize, skBounds, _disposable);
                    _skCanvas.DrawPath(skPath, skPaint);
                }

                if (SkiaUtil.IsValidStroke(svgPath))
                {
                    var skPaint = SkiaUtil.GetStrokeSKPaint(svgPath, _skSize, skBounds, _disposable);
                    _skCanvas.DrawPath(skPath, skPaint);
                }

                DrawMarkers(svgPath, skPath);
            }

            if (skPaintFilter != null)
            {
                _skCanvas.Restore();
            }

            if (skPaintOpacity != null)
            {
                _skCanvas.Restore();
            }

            _skCanvas.Restore();
        }

        public void DrawPolyline(SvgPolyline svgPolyline, bool ignoreDisplay)
        {
            if (!CanDraw(svgPolyline, ignoreDisplay))
            {
                return;
            }

            _skCanvas.Save();

            var skMatrix = SkiaUtil.GetSKMatrix(svgPolyline.Transforms);
            SetTransform(skMatrix);
            SetClipPath(svgPolyline, _disposable);

            var skPaintOpacity = SetOpacity(svgPolyline, _disposable);

            var skPaintFilter = SetFilter(svgPolyline, _disposable);

            var skPath = SkiaUtil.ToSKPath(svgPolyline.Points, svgPolyline.FillRule, false, _disposable);
            if (skPath != null && !skPath.IsEmpty)
            {
                var skBounds = skPath.Bounds;

                if (SkiaUtil.IsValidFill(svgPolyline))
                {
                    var skPaint = SkiaUtil.GetFillSKPaint(svgPolyline, _skSize, skBounds, _disposable);
                    _skCanvas.DrawPath(skPath, skPaint);
                }

                if (SkiaUtil.IsValidStroke(svgPolyline))
                {
                    var skPaint = SkiaUtil.GetStrokeSKPaint(svgPolyline, _skSize, skBounds, _disposable);
                    _skCanvas.DrawPath(skPath, skPaint);
                }

                DrawMarkers(svgPolyline, skPath);
            }

            if (skPaintFilter != null)
            {
                _skCanvas.Restore();
            }

            if (skPaintOpacity != null)
            {
                _skCanvas.Restore();
            }

            _skCanvas.Restore();
        }

        public void DrawPolygon(SvgPolygon svgPolygon, bool ignoreDisplay)
        {
            if (!CanDraw(svgPolygon, ignoreDisplay))
            {
                return;
            }

            _skCanvas.Save();

            var skMatrix = SkiaUtil.GetSKMatrix(svgPolygon.Transforms);
            SetTransform(skMatrix);
            SetClipPath(svgPolygon, _disposable);

            var skPaintOpacity = SetOpacity(svgPolygon, _disposable);

            var skPaintFilter = SetFilter(svgPolygon, _disposable);

            var skPath = SkiaUtil.ToSKPath(svgPolygon.Points, svgPolygon.FillRule, true, _disposable);
            if (skPath != null && !skPath.IsEmpty)
            {
                var skBounds = skPath.Bounds;

                if (SkiaUtil.IsValidFill(svgPolygon))
                {
                    var skPaint = SkiaUtil.GetFillSKPaint(svgPolygon, _skSize, skBounds, _disposable);
                    _skCanvas.DrawPath(skPath, skPaint);
                }

                if (SkiaUtil.IsValidStroke(svgPolygon))
                {
                    var skPaint = SkiaUtil.GetStrokeSKPaint(svgPolygon, _skSize, skBounds, _disposable);
                    _skCanvas.DrawPath(skPath, skPaint);
                }

                DrawMarkers(svgPolygon, skPath);
            }

            if (skPaintFilter != null)
            {
                _skCanvas.Restore();
            }

            if (skPaintOpacity != null)
            {
                _skCanvas.Restore();
            }

            _skCanvas.Restore();
        }

        public void DrawText(SvgText svgText, bool ignoreDisplay)
        {
            if (!CanDraw(svgText, ignoreDisplay))
            {
                return;
            }

            _skCanvas.Save();

            var skMatrix = SkiaUtil.GetSKMatrix(svgText.Transforms);
            SetTransform(skMatrix);
            SetClipPath(svgText, _disposable);

            var skPaintOpacity = SetOpacity(svgText, _disposable);

            var skPaintFilter = SetFilter(svgText, _disposable);

            // TODO:
            bool isValidFill = SkiaUtil.IsValidFill(svgText);
            bool isValidStroke = SkiaUtil.IsValidStroke(svgText);

            if (isValidFill || isValidStroke)
            {
                var text = svgText.Text?.Trim();

                if (svgText.X.Count == 1 && svgText.Y.Count == 1 && !string.IsNullOrEmpty(text))
                {
                    // TODO:
                    float x0 = svgText.X[0].ToDeviceValue(null, UnitRenderingType.HorizontalOffset, svgText);
                    float y0 = svgText.Y[0].ToDeviceValue(null, UnitRenderingType.VerticalOffset, svgText);

                    // TODO:
                    var skBounds = SKRect.Create(0f, 0f, _skSize.Width, _skSize.Height);

                    if (SkiaUtil.IsValidFill(svgText))
                    {
                        var skPaint = SkiaUtil.GetFillSKPaint(svgText, _skSize, skBounds, _disposable);
                        SkiaUtil.SetSKPaintText(svgText, _skSize, skBounds, skPaint, _disposable);
                        _skCanvas.DrawText(text, x0, y0, skPaint);
                    }

                    if (SkiaUtil.IsValidStroke(svgText))
                    {
                        var skPaint = SkiaUtil.GetStrokeSKPaint(svgText, _skSize, skBounds, _disposable);
                        SkiaUtil.SetSKPaintText(svgText, _skSize, skBounds, skPaint, _disposable);
                        _skCanvas.DrawText(text, x0, y0, skPaint);
                    }
                }
            }

            if (skPaintFilter != null)
            {
                _skCanvas.Restore();
            }

            if (skPaintOpacity != null)
            {
                _skCanvas.Restore();
            }

            _skCanvas.Restore();
        }

        public void DrawTextPath(SvgTextPath svgTextPath, bool ignoreDisplay)
        {
            if (!CanDraw(svgTextPath, ignoreDisplay))
            {
                return;
            }

            _skCanvas.Save();

            var skMatrix = SkiaUtil.GetSKMatrix(svgTextPath.Transforms);
            SetTransform(skMatrix);
            SetClipPath(svgTextPath, _disposable);

            var skPaintOpacity = SetOpacity(svgTextPath, _disposable);

            var skPaintFilter = SetFilter(svgTextPath, _disposable);

            // TODO:

            if (skPaintFilter != null)
            {
                _skCanvas.Restore();
            }

            if (skPaintOpacity != null)
            {
                _skCanvas.Restore();
            }

            _skCanvas.Restore();
        }

        public void DrawTextRef(SvgTextRef svgTextRef, bool ignoreDisplay)
        {
            if (!CanDraw(svgTextRef, ignoreDisplay))
            {
                return;
            }

            _skCanvas.Save();

            var skMatrix = SkiaUtil.GetSKMatrix(svgTextRef.Transforms);
            SetTransform(skMatrix);
            SetClipPath(svgTextRef, _disposable);

            var skPaintOpacity = SetOpacity(svgTextRef, _disposable);

            var skPaintFilter = SetFilter(svgTextRef, _disposable);

            // TODO:

            if (skPaintFilter != null)
            {
                _skCanvas.Restore();
            }

            if (skPaintOpacity != null)
            {
                _skCanvas.Restore();
            }

            _skCanvas.Restore();
        }

        public void DrawTextSpan(SvgTextSpan svgTextSpan, bool ignoreDisplay)
        {
            if (!CanDraw(svgTextSpan, ignoreDisplay))
            {
                return;
            }

            _skCanvas.Save();

            var skMatrix = SkiaUtil.GetSKMatrix(svgTextSpan.Transforms);
            SetTransform(skMatrix);
            SetClipPath(svgTextSpan, _disposable);

            var skPaintOpacity = SetOpacity(svgTextSpan, _disposable);

            var skPaintFilter = SetFilter(svgTextSpan, _disposable);

            // TODO:

            if (skPaintFilter != null)
            {
                _skCanvas.Restore();
            }

            if (skPaintOpacity != null)
            {
                _skCanvas.Restore();
            }

            _skCanvas.Restore();
        }
    }
}

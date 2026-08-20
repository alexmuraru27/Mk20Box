using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Mk20Box.Ui
{
    /// <summary>
    /// Works out which part of a picture survives a crop, so the editor can preview
    /// exactly what the theme composer will produce. Shared by key icons and the
    /// secondary screen, which differ only in shape.
    /// </summary>
    public sealed class CropPreview
    {
        private readonly double targetAspect;
        private double cachedAspect;
        private string cachedPath;

        public CropPreview(double targetWidth, double targetHeight)
        {
            targetAspect = targetWidth / targetHeight;
        }

        /// <summary>Whole picture padded, or filled and cropped.</summary>
        public Stretch StretchFor(bool fit)
        {
            return fit ? Stretch.Uniform : Stretch.Fill;
        }

        /// <summary>The visible part of the source, in relative units.</summary>
        public Rect Viewbox(string path, bool fit, double offsetX, double offsetY)
        {
            double sourceAspect = Aspect(path);

            if (fit || sourceAspect <= 0)
            {
                return new Rect(0, 0, 1, 1);
            }

            double width = 1;
            double height = 1;

            if (sourceAspect > targetAspect)
            {
                width = targetAspect / sourceAspect;
            }
            else
            {
                height = sourceAspect / targetAspect;
            }

            return new Rect(Place(offsetX, width), Place(offsetY, height), width, height);
        }

        /// <summary>True when the crop hides something, so panning has an effect.</summary>
        public bool CanPanX(string path, bool fit)
        {
            return Viewbox(path, fit, 0, 0).Width < 0.999;
        }

        public bool CanPanY(string path, bool fit)
        {
            return Viewbox(path, fit, 0, 0).Height < 0.999;
        }

        public void Invalidate()
        {
            cachedPath = null;
        }

        /// <summary>Centres the window on the panned point, kept inside the image.</summary>
        private static double Place(double offset, double size)
        {
            double centre = 0.5 + (offset / 2);
            double start = centre - (size / 2);
            double maximum = 1 - size;

            return start < 0 ? 0 : start > maximum ? maximum : start;
        }

        /// <summary>Width over height of the file, cached per path.</summary>
        private double Aspect(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !System.IO.File.Exists(path))
            {
                return 0;
            }

            if (string.Equals(cachedPath, path, StringComparison.OrdinalIgnoreCase))
            {
                return cachedAspect;
            }

            try
            {
                BitmapFrame frame = BitmapFrame.Create(
                    new Uri(path),
                    BitmapCreateOptions.DelayCreation,
                    BitmapCacheOption.None);

                cachedAspect = frame.PixelHeight > 0
                    ? (double)frame.PixelWidth / frame.PixelHeight
                    : 0;
            }
            catch (Exception)
            {
                cachedAspect = 0;
            }

            cachedPath = path;
            return cachedAspect;
        }

        /// <summary>Moves the window by a fraction of the target, for dragging.</summary>
        public static double Pan(double offset, double fraction)
        {
            double moved = offset + (fraction * 2);
            return moved < -1 ? -1 : moved > 1 ? 1 : moved;
        }
    }
}

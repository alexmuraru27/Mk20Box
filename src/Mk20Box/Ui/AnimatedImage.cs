using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Mk20Box.Ui
{
    /// <summary>
    /// Draws a picture, and plays it if it is an animated GIF. WPF's Image shows only
    /// the first frame, so frames are decoded and composited here.
    /// <see cref="Viewbox"/> selects the visible part of the source, matching the crop
    /// the theme composer applies.
    /// </summary>
    public sealed class AnimatedImage : FrameworkElement
    {
        public static readonly DependencyProperty SourcePathProperty =
            DependencyProperty.Register(
                nameof(SourcePath),
                typeof(string),
                typeof(AnimatedImage),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnSourceChanged));

        public static readonly DependencyProperty ViewboxProperty =
            DependencyProperty.Register(
                nameof(Viewbox),
                typeof(Rect),
                typeof(AnimatedImage),
                new FrameworkPropertyMetadata(new Rect(0, 0, 1, 1), FrameworkPropertyMetadataOptions.AffectsRender));

        /// <summary>How the frame fills this element; defaults to filling the viewbox.</summary>
        public static readonly DependencyProperty StretchProperty =
            DependencyProperty.Register(
                nameof(Stretch),
                typeof(Stretch),
                typeof(AnimatedImage),
                new FrameworkPropertyMetadata(Stretch.Fill, FrameworkPropertyMetadataOptions.AffectsRender));

        private readonly List<BitmapSource> frames = new List<BitmapSource>();
        private readonly List<int> delaysMs = new List<int>();
        private DispatcherTimer timer;
        private int frameIndex;

        public AnimatedImage()
        {
            IsVisibleChanged += OnIsVisibleChanged;
            Unloaded += (s, e) => StopTimer();
        }

        public string SourcePath
        {
            get { return (string)GetValue(SourcePathProperty); }
            set { SetValue(SourcePathProperty, value); }
        }

        public Rect Viewbox
        {
            get { return (Rect)GetValue(ViewboxProperty); }
            set { SetValue(ViewboxProperty, value); }
        }

        public Stretch Stretch
        {
            get { return (Stretch)GetValue(StretchProperty); }
            set { SetValue(StretchProperty, value); }
        }

        private static void OnSourceChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            ((AnimatedImage)sender).Reload();
        }

        private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            // No point burning CPU on a page the user cannot see.
            if (IsVisible)
            {
                StartTimer();
            }
            else
            {
                StopTimer();
            }
        }

        private void Reload()
        {
            StopTimer();
            frames.Clear();
            delaysMs.Clear();
            frameIndex = 0;

            string path = SourcePath;
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            {
                try
                {
                    Decode(path);
                }
                catch (Exception)
                {
                    // A broken or unsupported file simply draws nothing.
                    frames.Clear();
                    delaysMs.Clear();
                }
            }

            if (IsVisible)
            {
                StartTimer();
            }

            InvalidateVisual();
        }

        private void Decode(string path)
        {
            if (path.EndsWith(".gif", StringComparison.OrdinalIgnoreCase))
            {
                DecodeGif(path);
            }

            if (frames.Count == 0)
            {
                frames.Add(LoadStill(path));
                delaysMs.Add(0);
            }
        }

        private static BitmapSource LoadStill(string path)
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            image.UriSource = new Uri(path);
            image.EndInit();
            image.Freeze();
            return image;
        }

        /// <summary>
        /// Builds one finished bitmap per frame. GIF frames are often partial and carry
        /// a disposal rule, so each is drawn onto the previous result rather than shown
        /// on its own.
        /// </summary>
        private void DecodeGif(string path)
        {
            GifBitmapDecoder decoder;
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                decoder = new GifBitmapDecoder(
                    stream,
                    BitmapCreateOptions.PreservePixelFormat,
                    BitmapCacheOption.OnLoad);
            }

            if (decoder.Frames.Count <= 1)
            {
                return;
            }

            var screen = decoder.Metadata as BitmapMetadata;
            int width = ReadUShort(screen, "/logscrdesc/Width", decoder.Frames[0].PixelWidth);
            int height = ReadUShort(screen, "/logscrdesc/Height", decoder.Frames[0].PixelHeight);

            if (width <= 0 || height <= 0)
            {
                return;
            }

            // Frames are kept decoded, so a long or large GIF is scaled down to stay
            // within a sane budget. This is only the on-screen preview; the theme is
            // always built from the original file.
            double scale = PreviewScale(width, height, decoder.Frames.Count);

            BitmapSource canvas = null;
            BitmapSource restorePoint = null;

            foreach (BitmapFrame frame in decoder.Frames)
            {
                var meta = frame.Metadata as BitmapMetadata;
                int left = ReadUShort(meta, "/imgdesc/Left", 0);
                int top = ReadUShort(meta, "/imgdesc/Top", 0);
                int delay = ReadUShort(meta, "/grctlext/Delay", 0);
                int disposal = ReadByte(meta, "/grctlext/DisposalMethod", 0);

                BitmapSource previous = canvas;
                canvas = Compose(canvas, frame, left, top, width, height);

                frames.Add(Downscale(canvas, scale));

                // Browsers treat 0 and 1 hundredths as 10, so match that.
                delaysMs.Add(delay <= 1 ? 100 : delay * 10);

                if (disposal == 3)
                {
                    canvas = restorePoint ?? previous;
                }
                else if (disposal == 2)
                {
                    canvas = Erase(canvas, left, top, frame.PixelWidth, frame.PixelHeight, width, height);
                }
                else
                {
                    restorePoint = canvas;
                }
            }
        }

        /// <summary>Roughly 12 MB of pixels per animation, and never larger than needed.</summary>
        private static double PreviewScale(int width, int height, int frameCount)
        {
            const double MaxSide = 320;
            const double MaxBytes = 12 * 1024 * 1024;

            double scale = 1;
            double longest = Math.Max(width, height);

            if (longest > MaxSide)
            {
                scale = MaxSide / longest;
            }

            double bytes = width * scale * height * scale * 4 * Math.Max(1, frameCount);
            if (bytes > MaxBytes)
            {
                scale *= Math.Sqrt(MaxBytes / bytes);
            }

            return scale;
        }

        private static BitmapSource Downscale(BitmapSource source, double scale)
        {
            if (scale >= 0.999)
            {
                return source;
            }

            var scaled = new TransformedBitmap(source, new ScaleTransform(scale, scale));
            var cached = new CachedBitmap(scaled, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            cached.Freeze();
            return cached;
        }

        private static BitmapSource Compose(
            BitmapSource background,
            BitmapSource frame,
            int left,
            int top,
            int width,
            int height)
        {
            var visual = new DrawingVisual();
            using (DrawingContext context = visual.RenderOpen())
            {
                if (background != null)
                {
                    context.DrawImage(background, new Rect(0, 0, width, height));
                }

                context.DrawImage(frame, new Rect(left, top, frame.PixelWidth, frame.PixelHeight));
            }

            return Render(visual, width, height);
        }

        /// <summary>Clears the frame's own rectangle, for disposal method 2.</summary>
        private static BitmapSource Erase(
            BitmapSource background,
            int left,
            int top,
            int frameWidth,
            int frameHeight,
            int width,
            int height)
        {
            var visual = new DrawingVisual();
            using (DrawingContext context = visual.RenderOpen())
            {
                if (background != null)
                {
                    context.DrawImage(background, new Rect(0, 0, width, height));
                }

                context.PushClip(new RectangleGeometry(new Rect(left, top, frameWidth, frameHeight)));
                context.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, width, height));
                context.Pop();
            }

            return Render(visual, width, height);
        }

        private static BitmapSource Render(DrawingVisual visual, int width, int height)
        {
            var target = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            target.Render(visual);
            target.Freeze();
            return target;
        }

        private static int ReadUShort(BitmapMetadata metadata, string query, int fallback)
        {
            object value = ReadQuery(metadata, query);
            return value is ushort ? (ushort)value : fallback;
        }

        private static int ReadByte(BitmapMetadata metadata, string query, int fallback)
        {
            object value = ReadQuery(metadata, query);
            return value is byte ? (byte)value : fallback;
        }

        private static object ReadQuery(BitmapMetadata metadata, string query)
        {
            if (metadata == null)
            {
                return null;
            }

            try
            {
                return metadata.GetQuery(query);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private void StartTimer()
        {
            if (timer != null || frames.Count <= 1)
            {
                return;
            }

            timer = new DispatcherTimer(DispatcherPriority.Render);
            timer.Tick += OnTick;
            timer.Interval = TimeSpan.FromMilliseconds(delaysMs[frameIndex]);
            timer.Start();
        }

        private void StopTimer()
        {
            if (timer == null)
            {
                return;
            }

            timer.Stop();
            timer.Tick -= OnTick;
            timer = null;
        }

        private void OnTick(object sender, EventArgs e)
        {
            frameIndex = (frameIndex + 1) % frames.Count;
            timer.Interval = TimeSpan.FromMilliseconds(delaysMs[frameIndex]);
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);

            if (frames.Count == 0 || ActualWidth <= 0 || ActualHeight <= 0)
            {
                return;
            }

            var brush = new ImageBrush(frames[frameIndex])
            {
                Viewbox = Viewbox,
                ViewboxUnits = BrushMappingMode.RelativeToBoundingBox,
                Stretch = Stretch,
            };

            drawingContext.DrawRectangle(brush, null, new Rect(0, 0, ActualWidth, ActualHeight));
        }
    }
}

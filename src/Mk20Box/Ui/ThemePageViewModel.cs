using System;
using System.Collections.ObjectModel;
using System.Linq;
using Mk20Box.Layout;

namespace Mk20Box.Ui
{
    /// <summary>
    /// Editable view over one <see cref="Mk20PageSettings"/>. Pages have no name:
    /// they are identified by position, or by the key that opens them.
    /// </summary>
    public sealed class ThemePageViewModel : ViewModelBase
    {
        private readonly Mk20PageSettings model;

        public ThemePageViewModel(Mk20PageSettings model)
        {
            this.model = model ?? throw new ArgumentNullException(nameof(model));

            Keys = new ObservableCollection<DeviceKeyViewModel>(
                model.Keys
                    .OrderBy(key => key.Row)
                    .ThenBy(key => key.Column)
                    .Select(key => new DeviceKeyViewModel(key)));

            if (model.LeftEncoder == null)
            {
                model.LeftEncoder = new Mk20EncoderSettings();
            }

            if (model.RightEncoder == null)
            {
                model.RightEncoder = new Mk20EncoderSettings();
            }

            LeftEncoder = new EncoderViewModel(model.LeftEncoder, isLeft: true);
            RightEncoder = new EncoderViewModel(model.RightEncoder, isLeft: false);
        }

        public Mk20PageSettings Model => model;

        public string Id => model.Id;

        public string ParentPageId => model.ParentPageId;

        public bool IsFolder => !string.IsNullOrEmpty(model.ParentPageId);

        public ObservableCollection<DeviceKeyViewModel> Keys { get; }

        public EncoderViewModel LeftEncoder { get; }

        public EncoderViewModel RightEncoder { get; }

        /// <summary>Image or GIF filling the 428x142 secondary strip.</summary>
        public string SecondaryBackgroundPath
        {
            get { return model.SecondaryBackgroundPath; }
            set
            {
                if (model.SecondaryBackgroundPath != value)
                {
                    model.SecondaryBackgroundPath = value;
                    cachedAspectPath = null;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(HasSecondaryBackground));
                    OnPropertyChanged(nameof(SecondaryBackgroundViewbox));
                    OnPropertyChanged(nameof(CanPanSecondaryX));
                    OnPropertyChanged(nameof(CanPanSecondaryY));
                }
            }
        }

        /// <summary>True when the crop hides part of the source, so panning does something.</summary>
        public bool CanPanSecondaryX => SecondaryBackgroundViewbox.Width < 0.999;

        public bool CanPanSecondaryY => SecondaryBackgroundViewbox.Height < 0.999;

        /// <summary>Drag hint shown over the strip.</summary>
        public string SecondaryPanHint
        {
            get
            {
                if (!HasSecondaryBackground)
                {
                    return string.Empty;
                }

                if (CanPanSecondaryX && CanPanSecondaryY)
                {
                    return "drag to reposition";
                }

                if (CanPanSecondaryX)
                {
                    return "drag sideways to reposition";
                }

                return CanPanSecondaryY ? "drag up or down to reposition" : "fills exactly";
            }
        }

        public bool HasSecondaryBackground =>
            !string.IsNullOrWhiteSpace(model.SecondaryBackgroundPath);

        /// <summary>Crop pan, -1 hard left to +1 hard right.</summary>
        public double SecondaryBackgroundOffsetX
        {
            get { return model.SecondaryBackgroundOffsetX; }
            set
            {
                if (Math.Abs(model.SecondaryBackgroundOffsetX - value) > 0.001)
                {
                    model.SecondaryBackgroundOffsetX = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(SecondaryBackgroundViewbox));
                }
            }
        }

        /// <summary>Crop pan, -1 hard top to +1 hard bottom.</summary>
        public double SecondaryBackgroundOffsetY
        {
            get { return model.SecondaryBackgroundOffsetY; }
            set
            {
                if (Math.Abs(model.SecondaryBackgroundOffsetY - value) > 0.001)
                {
                    model.SecondaryBackgroundOffsetY = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(SecondaryBackgroundViewbox));
                }
            }
        }

        /// <summary>Moves the visible window by a fraction of the strip, for dragging.</summary>
        public void PanSecondaryBackground(double fractionX, double fractionY)
        {
            SecondaryBackgroundOffsetX = Limit(SecondaryBackgroundOffsetX + (fractionX * 2));
            SecondaryBackgroundOffsetY = Limit(SecondaryBackgroundOffsetY + (fractionY * 2));
        }

        private static double Limit(double offset)
        {
            return offset < -1 ? -1 : offset > 1 ? 1 : offset;
        }

        /// <summary>
        /// The part of the source that survives the crop, in relative units, so the
        /// preview shows exactly what the composer will send. Mirrors ImageSharp's
        /// crop-to-fill with a panned centre.
        /// </summary>
        public System.Windows.Rect SecondaryBackgroundViewbox
        {
            get
            {
                const double TargetAspect = 428.0 / 142.0;
                double sourceAspect = SourceAspect();

                if (sourceAspect <= 0)
                {
                    return new System.Windows.Rect(0, 0, 1, 1);
                }

                double width = 1;
                double height = 1;

                if (sourceAspect > TargetAspect)
                {
                    width = TargetAspect / sourceAspect;
                }
                else
                {
                    height = sourceAspect / TargetAspect;
                }

                double x = Place(SecondaryBackgroundOffsetX, width);
                double y = Place(SecondaryBackgroundOffsetY, height);
                return new System.Windows.Rect(x, y, width, height);
            }
        }

        /// <summary>Centres the window on the panned point, kept inside the image.</summary>
        private static double Place(double offset, double size)
        {
            double centre = 0.5 + (offset / 2);
            double start = centre - (size / 2);
            double maximum = 1 - size;

            return start < 0 ? 0 : start > maximum ? maximum : start;
        }

        private double cachedAspect;
        private string cachedAspectPath;

        /// <summary>Width over height of the chosen file, cached per path.</summary>
        private double SourceAspect()
        {
            string path = model.SecondaryBackgroundPath;
            if (string.IsNullOrWhiteSpace(path) || !System.IO.File.Exists(path))
            {
                return 0;
            }

            if (string.Equals(cachedAspectPath, path, StringComparison.OrdinalIgnoreCase))
            {
                return cachedAspect;
            }

            try
            {
                var frame = System.Windows.Media.Imaging.BitmapFrame.Create(
                    new Uri(path),
                    System.Windows.Media.Imaging.BitmapCreateOptions.DelayCreation,
                    System.Windows.Media.Imaging.BitmapCacheOption.None);

                cachedAspect = frame.PixelHeight > 0
                    ? (double)frame.PixelWidth / frame.PixelHeight
                    : 0;
            }
            catch (Exception)
            {
                cachedAspect = 0;
            }

            cachedAspectPath = path;
            return cachedAspect;
        }
    }
}

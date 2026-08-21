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

            if (model.Widgets == null)
            {
                model.Widgets = new System.Collections.Generic.List<Mk20WidgetSettings>();
            }

            foreach (Mk20WidgetSettings widget in model.Widgets)
            {
                Widgets.Add(Watch(new WidgetViewModel(widget)));
            }
        }

        /// <summary>Keeps the stored list in step when a widget changes kind.</summary>
        private WidgetViewModel Watch(WidgetViewModel widget)
        {
            widget.Replaced += (sender, e) =>
            {
                int index = model.Widgets.IndexOf(e.OldWidget);
                if (index >= 0)
                {
                    model.Widgets[index] = e.NewWidget;
                }
            };

            return widget;
        }

        /// <summary>Widgets drawn on the secondary screen.</summary>
        public ObservableCollection<WidgetViewModel> Widgets { get; }
            = new ObservableCollection<WidgetViewModel>();

        /// <summary>Adds a widget to both the view and the persisted model.</summary>
        public WidgetViewModel AddWidget(Mk20WidgetSettings widget)
        {
            model.Widgets.Add(widget);
            WidgetViewModel viewModel = Watch(new WidgetViewModel(widget));
            Widgets.Add(viewModel);
            OnPropertyChanged(nameof(HasWidgets));
            return viewModel;
        }

        public void RemoveWidget(WidgetViewModel widget)
        {
            if (widget == null)
            {
                return;
            }

            model.Widgets.Remove(widget.Model);
            Widgets.Remove(widget);
            OnPropertyChanged(nameof(HasWidgets));
        }

        public bool HasWidgets => Widgets.Count > 0;

        public Mk20PageSettings Model => model;

        public string Id => model.Id;

        public string ParentPageId => model.ParentPageId;

        public bool IsFolder => !string.IsNullOrEmpty(model.ParentPageId);

        public ObservableCollection<DeviceKeyViewModel> Keys { get; }

        public EncoderViewModel LeftEncoder { get; }

        public EncoderViewModel RightEncoder { get; }

        private readonly CropPreview crop = new CropPreview(428, 142);

        /// <summary>Image or GIF filling the 428x142 secondary strip.</summary>
        public string SecondaryBackgroundPath
        {
            get { return model.SecondaryBackgroundPath; }
            set
            {
                if (model.SecondaryBackgroundPath != value)
                {
                    model.SecondaryBackgroundPath = value;
                    crop.Invalidate();
                    RaiseSecondaryChanged();
                }
            }
        }

        public bool HasSecondaryBackground =>
            !string.IsNullOrWhiteSpace(model.SecondaryBackgroundPath);

        /// <summary>Whole picture padded, or filled and cropped.</summary>
        public bool SecondaryBackgroundFit
        {
            get { return model.SecondaryBackgroundFit; }
            set
            {
                if (model.SecondaryBackgroundFit != value)
                {
                    model.SecondaryBackgroundFit = value;
                    RaiseSecondaryChanged();
                }
            }
        }

        public System.Windows.Media.Stretch SecondaryStretch =>
            crop.StretchFor(SecondaryBackgroundFit);

        public System.Windows.Rect SecondaryBackgroundViewbox => crop.Viewbox(
            model.SecondaryBackgroundPath,
            SecondaryBackgroundFit,
            model.SecondaryBackgroundOffsetX,
            model.SecondaryBackgroundOffsetY);

        public bool CanPanSecondary =>
            crop.CanPanX(model.SecondaryBackgroundPath, SecondaryBackgroundFit)
            || crop.CanPanY(model.SecondaryBackgroundPath, SecondaryBackgroundFit);

        /// <summary>Hint shown over the strip.</summary>
        public string SecondaryPanHint
        {
            get
            {
                if (!HasSecondaryBackground)
                {
                    return string.Empty;
                }

                if (SecondaryBackgroundFit)
                {
                    return "whole picture";
                }

                return CanPanSecondary ? "drag to reposition" : "fills exactly";
            }
        }

        public double SecondaryBackgroundOffsetX
        {
            get { return model.SecondaryBackgroundOffsetX; }
            set
            {
                if (Math.Abs(model.SecondaryBackgroundOffsetX - value) > 0.001)
                {
                    model.SecondaryBackgroundOffsetX = value;
                    RaiseSecondaryChanged();
                }
            }
        }

        public double SecondaryBackgroundOffsetY
        {
            get { return model.SecondaryBackgroundOffsetY; }
            set
            {
                if (Math.Abs(model.SecondaryBackgroundOffsetY - value) > 0.001)
                {
                    model.SecondaryBackgroundOffsetY = value;
                    RaiseSecondaryChanged();
                }
            }
        }

        /// <summary>Moves the visible window by a fraction of the strip, for dragging.</summary>
        public void PanSecondaryBackground(double fractionX, double fractionY)
        {
            SecondaryBackgroundOffsetX = CropPreview.Pan(SecondaryBackgroundOffsetX, fractionX);
            SecondaryBackgroundOffsetY = CropPreview.Pan(SecondaryBackgroundOffsetY, fractionY);
        }

        private void RaiseSecondaryChanged()
        {
            OnPropertyChanged(nameof(SecondaryBackgroundPath));
            OnPropertyChanged(nameof(HasSecondaryBackground));
            OnPropertyChanged(nameof(SecondaryBackgroundFit));
            OnPropertyChanged(nameof(SecondaryStretch));
            OnPropertyChanged(nameof(SecondaryBackgroundViewbox));
            OnPropertyChanged(nameof(SecondaryBackgroundOffsetX));
            OnPropertyChanged(nameof(SecondaryBackgroundOffsetY));
            OnPropertyChanged(nameof(CanPanSecondary));
            OnPropertyChanged(nameof(SecondaryPanHint));
        }    }
}

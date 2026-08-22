using System;
using Mk20Box.Layout;

namespace Mk20Box.Ui
{
    /// <summary>Editable view over one <see cref="Mk20KeySettings"/>.</summary>
    public sealed class DeviceKeyViewModel : ViewModelBase
    {
        private readonly Mk20KeySettings model;

        public DeviceKeyViewModel(Mk20KeySettings model)
        {
            this.model = model ?? throw new ArgumentNullException(nameof(model));
            Number = (model.Row * DeviceLayout.Columns) + model.Column + 1;

            if (model.Keystroke == null)
            {
                model.Keystroke = new Mk20KeystrokeSettings();
            }

            if (model.MacroSteps == null)
            {
                model.MacroSteps = new System.Collections.Generic.List<Mk20MacroStepSettings>();
            }

            foreach (Mk20MacroStepSettings step in model.MacroSteps)
            {
                MacroSteps.Add(new MacroStepViewModel(step));
            }
        }

        /// <summary>Appends a step to both the view and the persisted model.</summary>
        public MacroStepViewModel AddMacroStep(Mk20MacroStepSettings step)
        {
            model.MacroSteps.Add(step);
            var viewModel = new MacroStepViewModel(step);
            MacroSteps.Add(viewModel);
            return viewModel;
        }

        public void RemoveMacroStep(MacroStepViewModel step)
        {
            if (step == null)
            {
                return;
            }

            model.MacroSteps.Remove(step.Model);
            MacroSteps.Remove(step);
        }

        /// <summary>Reorders a step, keeping the view and the persisted list in sync.</summary>
        public bool MoveMacroStep(MacroStepViewModel step, int offset)
        {
            if (step == null)
            {
                return false;
            }

            int from = MacroSteps.IndexOf(step);
            int to = from + offset;

            if (from < 0 || to < 0 || to >= MacroSteps.Count)
            {
                return false;
            }

            MacroSteps.Move(from, to);
            model.MacroSteps.Remove(step.Model);
            model.MacroSteps.Insert(to, step.Model);
            return true;
        }

        /// <summary>The underlying persisted key.</summary>
        public Mk20KeySettings Model => model;

        public int Number { get; }

        public int Row => model.Row;

        public int Column => model.Column;

        public string Name => string.Format("Key {0:00}", Number);

        public string Label => string.Format("KEY {0:00}", Number);

        public string Position => string.Format("row {0}, column {1}", Row, Column);

        public string Title
        {
            get { return model.Title; }
            set
            {
                if (model.Title != value)
                {
                    model.Title = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(HasTitle));
                }
            }
        }

        public bool HasTitle => !string.IsNullOrWhiteSpace(model.Title);

        /// <summary>Preview brush so the schematic shows the chosen colour.</summary>
        public System.Windows.Media.Brush TitleBrush
        {
            get
            {
                try
                {
                    var converted = System.Windows.Media.ColorConverter.ConvertFromString(TitleColor);
                    return new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)converted);
                }
                catch (FormatException)
                {
                    return System.Windows.Media.Brushes.White;
                }
            }
        }

        /// <summary>Preview placement, mirroring the device's top/bottom alignment.</summary>
        public System.Windows.VerticalAlignment TitleAlignment
        {
            get
            {
                return string.Equals(TitlePosition, "top", StringComparison.OrdinalIgnoreCase)
                    ? System.Windows.VerticalAlignment.Top
                    : System.Windows.VerticalAlignment.Bottom;
            }
        }

        /// <summary>Title size in points.</summary>
        public double TitleFontSize
        {
            get { return model.TitleFontSize > 0 ? model.TitleFontSize : KeyTitleDefaults.FontSize; }
            set
            {
                if (Math.Abs(model.TitleFontSize - value) > 0.01)
                {
                    model.TitleFontSize = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>Title colour as #rrggbb.</summary>
        public string TitleColor
        {
            get
            {
                return string.IsNullOrWhiteSpace(model.TitleColor)
                    ? KeyTitleDefaults.Color
                    : model.TitleColor;
            }

            set
            {
                if (model.TitleColor != value)
                {
                    model.TitleColor = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(TitleBrush));
                }
            }
        }

        /// <summary>Where the text sits on the key: "bottom" or "top".</summary>
        public string TitlePosition
        {
            get
            {
                return string.IsNullOrWhiteSpace(model.TitlePosition)
                    ? KeyTitleDefaults.Position
                    : model.TitlePosition;
            }

            set
            {
                if (model.TitlePosition != value)
                {
                    model.TitlePosition = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(TitleAlignment));
                }
            }
        }

        /// <summary>Picture or GIF drawn on this key.</summary>
        public string MediaPath
        {
            get { return model.MediaPath; }
            set
            {
                if (model.MediaPath == value)
                {
                    return;
                }

                model.MediaPath = value;
                crop.Invalidate();
                RaiseIconChanged();
            }
        }

        public bool HasMedia => !string.IsNullOrWhiteSpace(model.MediaPath);

        private readonly CropPreview crop = new CropPreview(128, 128);

        /// <summary>Whole picture padded, or filled and cropped.</summary>
        public bool IconFit
        {
            get { return model.IconFit; }
            set
            {
                if (model.IconFit != value)
                {
                    model.IconFit = value;
                    RaiseIconChanged();
                }
            }
        }

        public System.Windows.Media.Stretch IconStretch => crop.StretchFor(IconFit);

        public System.Windows.Rect IconViewbox =>
            crop.Viewbox(model.MediaPath, IconFit, model.IconOffsetX, model.IconOffsetY);

        public bool CanPanIcon =>
            crop.CanPanX(model.MediaPath, IconFit) || crop.CanPanY(model.MediaPath, IconFit);

        public double IconOffsetX
        {
            get { return model.IconOffsetX; }
            set
            {
                if (Math.Abs(model.IconOffsetX - value) > 0.001)
                {
                    model.IconOffsetX = value;
                    RaiseIconChanged();
                }
            }
        }

        public double IconOffsetY
        {
            get { return model.IconOffsetY; }
            set
            {
                if (Math.Abs(model.IconOffsetY - value) > 0.001)
                {
                    model.IconOffsetY = value;
                    RaiseIconChanged();
                }
            }
        }

        /// <summary>Moves the visible window by a fraction of the key, for dragging.</summary>
        public void PanIcon(double fractionX, double fractionY)
        {
            IconOffsetX = CropPreview.Pan(IconOffsetX, fractionX);
            IconOffsetY = CropPreview.Pan(IconOffsetY, fractionY);
        }

        private void RaiseIconChanged()
        {
            OnPropertyChanged(nameof(MediaPath));
            OnPropertyChanged(nameof(HasMedia));
            OnPropertyChanged(nameof(IconFit));
            OnPropertyChanged(nameof(IconStretch));
            OnPropertyChanged(nameof(IconViewbox));
            OnPropertyChanged(nameof(IconOffsetX));
            OnPropertyChanged(nameof(IconOffsetY));
            OnPropertyChanged(nameof(CanPanIcon));
        }

        public string ActionType
        {
            get { return model.ActionType; }
            set
            {
                if (model.ActionType == value)
                {
                    return;
                }

                // The folder is dropped by the layout, which can also delete it and ask
                // first. Clearing the link here would strand the pages instead.
                model.ActionType = value;

                OnPropertyChanged();
                RaiseActionChanged();
            }
        }

        public string TargetPageId
        {
            get { return model.TargetPageId; }
            set
            {
                if (model.TargetPageId == value)
                {
                    return;
                }

                model.TargetPageId = value;
                OnPropertyChanged();
                RaiseActionChanged();
            }
        }

        /// <summary>SimHub action/macro name for non-navigation keys.</summary>
        public string ActionTarget
        {
            get { return model.ActionTarget; }
            set
            {
                if (model.ActionTarget != value)
                {
                    model.ActionTarget = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool HasAction => model.ActionType != KeyActionKinds.Unassigned;

        public bool IsNavigation => KeyActionKinds.IsNavigation(model.ActionType);

        public bool OpensFolder => model.ActionType == KeyActionKinds.OpenFolder;

        /// <summary>The device types this itself; no host involvement.</summary>
        public bool SendsKeystroke => model.ActionType == KeyActionKinds.KeyboardKey;

        /// <summary>The plugin replays a step sequence for this key.</summary>
        public bool RunsMacro => model.ActionType == KeyActionKinds.Macro;

        /// <summary>The device reports the press and the plugin acts on it.</summary>
        public bool IsHostRouted =>
            model.ActionType == KeyActionKinds.SimHubAction
            || model.ActionType == KeyActionKinds.SimHubInput;

        public Mk20KeystrokeSettings Keystroke
        {
            get { return model.Keystroke; }
            set
            {
                model.Keystroke = value ?? new Mk20KeystrokeSettings();
                OnPropertyChanged();
            }
        }

        public System.Collections.ObjectModel.ObservableCollection<MacroStepViewModel> MacroSteps { get; }
            = new System.Collections.ObjectModel.ObservableCollection<MacroStepViewModel>();

        public string NavigationGlyph => KeyActionKinds.GlyphFor(model.ActionType);

        public bool HasNavigationGlyph => NavigationGlyph != null;

        /// <summary>Refreshes everything derived from the action, including the summary.</summary>
        public void RaiseActionChanged()
        {
            OnPropertyChanged(nameof(HasAction));
            OnPropertyChanged(nameof(IsNavigation));
            OnPropertyChanged(nameof(OpensFolder));
            OnPropertyChanged(nameof(SendsKeystroke));
            OnPropertyChanged(nameof(RunsMacro));
            OnPropertyChanged(nameof(IsHostRouted));
            OnPropertyChanged(nameof(NavigationGlyph));
            OnPropertyChanged(nameof(HasNavigationGlyph));
        }

        /// <summary>Takes on a copied key's look and action, keeping this key's cell.</summary>
        public void ApplyFrom(Mk20KeySettings source)
        {
            if (source == null)
            {
                return;
            }

            model.ApplyFrom(source);
            ReloadFromModel();
        }

        /// <summary>Clears the key back to blank and unassigned.</summary>
        public void ResetToDefault()
        {
            model.Reset();
            ReloadFromModel();
        }

        /// <summary>
        /// Re-reads every value from the model after it was replaced wholesale, which
        /// property setters alone cannot announce.
        /// </summary>
        private void ReloadFromModel()
        {
            MacroSteps.Clear();
            foreach (Mk20MacroStepSettings step in model.MacroSteps)
            {
                MacroSteps.Add(new MacroStepViewModel(step));
            }

            crop.Invalidate();

            OnPropertyChanged(nameof(Title));
            OnPropertyChanged(nameof(HasTitle));
            OnPropertyChanged(nameof(TitleBrush));
            OnPropertyChanged(nameof(TitleAlignment));
            OnPropertyChanged(nameof(TitleFontSize));
            OnPropertyChanged(nameof(TitleColor));
            OnPropertyChanged(nameof(TitlePosition));
            OnPropertyChanged(nameof(Keystroke));
            OnPropertyChanged(nameof(TargetPageId));
            OnPropertyChanged(nameof(ActionTarget));

            RaiseIconChanged();

            // Last, so the layout sees a fully rebuilt key when it reacts to the
            // action changing by creating a folder for it.
            OnPropertyChanged(nameof(ActionType));
            RaiseActionChanged();
        }
    }
}

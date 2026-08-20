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
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasMedia));
            }
        }

        public bool HasMedia => !string.IsNullOrWhiteSpace(model.MediaPath);

        public string ActionType
        {
            get { return model.ActionType; }
            set
            {
                if (model.ActionType == value)
                {
                    return;
                }

                model.ActionType = value;

                if (value != KeyActionKinds.OpenFolder)
                {
                    model.TargetPageId = null;
                }

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
    }
}

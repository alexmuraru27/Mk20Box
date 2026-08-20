using System.Collections.Generic;
using Mk20Box.Layout;

namespace Mk20Box.Ui
{
    /// <summary>Editable view over one encoder on a page.</summary>
    public sealed class EncoderViewModel : ViewModelBase
    {
        private readonly Mk20EncoderSettings model;

        public EncoderViewModel(Mk20EncoderSettings model, bool isLeft)
        {
            this.model = model;
            IsLeft = isLeft;
        }

        public Mk20EncoderSettings Model => model;

        public bool IsLeft { get; }

        public string Name => IsLeft ? "Left encoder" : "Right encoder";

        public IReadOnlyList<string> Modes => EncoderModes.All;

        public IReadOnlyList<string> Functions => EncoderFunctions.All;

        public string Mode
        {
            get { return model.Mode; }
            set
            {
                if (model.Mode == value)
                {
                    return;
                }

                model.Mode = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(UsesFunction));
                OnPropertyChanged(nameof(UsesKeystrokes));
                OnPropertyChanged(nameof(ReportsToPlugin));
                OnPropertyChanged(nameof(ShowsDirectionWarning));
                OnPropertyChanged(nameof(Summary));
            }
        }

        public string Function
        {
            get { return model.Function; }
            set
            {
                if (model.Function != value)
                {
                    model.Function = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(Summary));
                }
            }
        }

        public Mk20KeystrokeSettings RotateLeft
        {
            get { return model.RotateLeft; }
            set { model.RotateLeft = value; OnPropertyChanged(); OnPropertyChanged(nameof(Summary)); }
        }

        public Mk20KeystrokeSettings Click
        {
            get { return model.Click; }
            set { model.Click = value; OnPropertyChanged(); OnPropertyChanged(nameof(Summary)); }
        }

        public Mk20KeystrokeSettings RotateRight
        {
            get { return model.RotateRight; }
            set { model.RotateRight = value; OnPropertyChanged(); OnPropertyChanged(nameof(Summary)); }
        }

        public string CommandId
        {
            get { return model.CommandId; }
            set { model.CommandId = value; OnPropertyChanged(); OnPropertyChanged(nameof(Summary)); }
        }

        public bool UsesFunction => model.Mode == EncoderModes.BuiltInFunction;

        public bool UsesKeystrokes => model.Mode == EncoderModes.Keystrokes;

        public bool ReportsToPlugin => model.Mode == EncoderModes.ReportToPlugin;

        /// <summary>Report-to-plugin cannot distinguish rotation direction.</summary>
        public bool ShowsDirectionWarning => ReportsToPlugin;

        public string Summary => model.Describe();
    }
}

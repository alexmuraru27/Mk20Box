using Mk20Box.Layout;

namespace Mk20Box.Ui
{
    /// <summary>Editable view over one macro step.</summary>
    public sealed class MacroStepViewModel : ViewModelBase
    {
        private readonly Mk20MacroStepSettings model;

        public MacroStepViewModel(Mk20MacroStepSettings model)
        {
            this.model = model;
        }

        public Mk20MacroStepSettings Model => model;

        public string Kind => model.Kind;

        public Mk20KeystrokeSettings Keystroke
        {
            get { return model.Keystroke; }
            set
            {
                model.Keystroke = value ?? new Mk20KeystrokeSettings();
                OnPropertyChanged();
                OnPropertyChanged(nameof(Description));
            }
        }

        public string Text
        {
            get { return model.Text; }
            set
            {
                model.Text = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Description));
            }
        }

        public int DelayMs
        {
            get { return model.DelayMs; }
            set
            {
                model.DelayMs = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Description));
            }
        }

        public string Description => model.Describe();

        public bool IsKeystroke => model.Kind == MacroStepKinds.Keystroke;

        public bool IsText => model.Kind == MacroStepKinds.Text;

        public bool IsDelay => model.Kind == MacroStepKinds.Delay;

        public bool IsSimHubAction => model.Kind == MacroStepKinds.SimHubAction;

        public string ActionName
        {
            get { return model.ActionName; }
            set
            {
                model.ActionName = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Description));
            }
        }
    }
}

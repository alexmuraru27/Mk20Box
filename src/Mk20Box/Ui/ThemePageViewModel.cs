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
    }
}

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Mk20Box.Layout;
using Mk20Control.Protocol.Theme.Building;

namespace Mk20Box.Ui
{
    /// <summary>
    /// Records a keystroke by listening for a real key press, rather than making the
    /// user hunt through a list. Click to arm, press the combination, Escape cancels.
    /// </summary>
    public sealed class KeystrokeRecorder : Button
    {
        public static readonly DependencyProperty KeystrokeProperty =
            DependencyProperty.Register(
                nameof(Keystroke),
                typeof(Mk20KeystrokeSettings),
                typeof(KeystrokeRecorder),
                new FrameworkPropertyMetadata(
                    null,
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    OnKeystrokeChanged));

        private static readonly DependencyPropertyKey IsRecordingPropertyKey =
            DependencyProperty.RegisterReadOnly(
                nameof(IsRecording),
                typeof(bool),
                typeof(KeystrokeRecorder),
                new PropertyMetadata(false));

        /// <summary>True while waiting for a key press, so the template can highlight.</summary>
        public static readonly DependencyProperty IsRecordingProperty =
            IsRecordingPropertyKey.DependencyProperty;

        private static readonly DependencyPropertyKey HasKeystrokePropertyKey =
            DependencyProperty.RegisterReadOnly(
                nameof(HasKeystroke),
                typeof(bool),
                typeof(KeystrokeRecorder),
                new PropertyMetadata(false));

        public static readonly DependencyProperty HasKeystrokeProperty =
            HasKeystrokePropertyKey.DependencyProperty;

        private bool isRecording;

        public KeystrokeRecorder()
        {
            Focusable = true;
            UpdateText();
        }

        /// <summary>Raised once a keystroke has been captured or cleared.</summary>
        public event RoutedEventHandler Recorded;

        public bool IsRecording
        {
            get { return (bool)GetValue(IsRecordingProperty); }
        }

        public bool HasKeystroke
        {
            get { return (bool)GetValue(HasKeystrokeProperty); }
        }

        public Mk20KeystrokeSettings Keystroke
        {
            get { return (Mk20KeystrokeSettings)GetValue(KeystrokeProperty); }
            set { SetValue(KeystrokeProperty, value); }
        }

        protected override void OnClick()
        {
            base.OnClick();
            isRecording = true;
            Focus();
            Keyboard.Focus(this);
            UpdateText();
        }

        protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e)
        {
            base.OnLostKeyboardFocus(e);
            if (isRecording)
            {
                isRecording = false;
                UpdateText();
            }
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            if (!isRecording)
            {
                base.OnPreviewKeyDown(e);
                return;
            }

            e.Handled = true;

            // Alt combinations arrive as SystemKey.
            Key key = e.Key == Key.System ? e.SystemKey : e.Key;

            if (key == Key.Escape)
            {
                isRecording = false;
                UpdateText();
                return;
            }

            if (KeyMapping.IsModifier(key))
            {
                // Held on its own it may still be the wanted key, e.g. ETS2 shifts on
                // bare Shift and Ctrl. Decided on release, once it is clear no other
                // key followed.
                pendingModifier = key;
                Content = "Release for " + Describe(key) + ", or press another key";
                return;
            }

            pendingModifier = Key.None;

            HidKey hidKey;
            if (!KeyMapping.TryMap(key, out hidKey))
            {
                Content = "Unsupported key - try another";
                return;
            }

            ModifierKeys modifiers = Keyboard.Modifiers;

            Keystroke = new Mk20KeystrokeSettings
            {
                Ctrl = (modifiers & ModifierKeys.Control) != 0,
                Shift = (modifiers & ModifierKeys.Shift) != 0,
                Alt = (modifiers & ModifierKeys.Alt) != 0,
                Win = (modifiers & ModifierKeys.Windows) != 0,
                Key = hidKey.ToString(),
            };

            isRecording = false;
            UpdateText();
            Recorded?.Invoke(this, new RoutedEventArgs());
        }

        /// <summary>
        /// A modifier released without any other key becomes the keystroke itself,
        /// rather than a modifier of something else.
        /// </summary>
        protected override void OnPreviewKeyUp(KeyEventArgs e)
        {
            if (!isRecording || pendingModifier == Key.None)
            {
                base.OnPreviewKeyUp(e);
                return;
            }

            Key key = e.Key == Key.System ? e.SystemKey : e.Key;
            if (key != pendingModifier)
            {
                base.OnPreviewKeyUp(e);
                return;
            }

            e.Handled = true;

            HidKey hidKey;
            if (KeyMapping.TryMap(key, out hidKey))
            {
                Keystroke = new Mk20KeystrokeSettings { Key = hidKey.ToString() };
                isRecording = false;
                pendingModifier = Key.None;
                UpdateText();
                Recorded?.Invoke(this, new RoutedEventArgs());
                return;
            }

            pendingModifier = Key.None;
        }

        private static string Describe(Key key)
        {
            switch (key)
            {
                case Key.LeftCtrl:
                case Key.RightCtrl:
                    return "Ctrl";

                case Key.LeftShift:
                case Key.RightShift:
                    return "Shift";

                case Key.LeftAlt:
                case Key.RightAlt:
                    return "Alt";

                default:
                    return "Win";
            }
        }

        private Key pendingModifier = Key.None;

        /// <summary>Forgets the recorded keystroke.</summary>
        public void Clear()
        {
            Keystroke = new Mk20KeystrokeSettings();
            isRecording = false;
            UpdateText();
            Recorded?.Invoke(this, new RoutedEventArgs());
        }

        private static void OnKeystrokeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((KeystrokeRecorder)d).UpdateText();
        }

        private void UpdateText()
        {
            SetValue(IsRecordingPropertyKey, isRecording);

            if (isRecording)
            {
                SetValue(HasKeystrokePropertyKey, false);
                Content = "Press a key combination...";
                return;
            }

            Mk20KeystrokeSettings keystroke = Keystroke;
            bool has = keystroke != null && keystroke.HasKey;

            SetValue(HasKeystrokePropertyKey, has);
            Content = has ? keystroke.ToString() : "Click to record";
        }
    }
}

using System.Collections.Generic;

namespace Mk20Box.Layout
{
    /// <summary>
    /// A profile's key layout, as stored in the plugin settings JSON. This is the
    /// source of truth that the theme composer turns into a device theme.
    /// </summary>
    public sealed class Mk20LayoutSettings
    {
        public List<Mk20PageSettings> Pages { get; set; } = new List<Mk20PageSettings>();

        /// <summary>
        /// Draw page 1's secondary screen on every page and folder. The strip is
        /// stored per page because the device stores it per page, so the composer
        /// replicates it rather than the editor duplicating data.
        /// </summary>
        public bool GlobalSecondaryScreen { get; set; }

        /// <summary>
        /// A complete vanilla layout: one page, all twenty keys blank, both encoders
        /// unassigned. Used for new profiles and when resetting an existing one.
        /// </summary>
        public static Mk20LayoutSettings CreateDefault()
        {
            var layout = new Mk20LayoutSettings();
            layout.Pages.Add(CreateEmptyPage(null));
            return layout;
        }

        /// <summary>
        /// Builds a page with every key present, so nothing is created lazily. Pages
        /// carry no name: the device identifies them by id, and a folder is known by
        /// the key that opens it.
        /// </summary>
        public static Mk20PageSettings CreateEmptyPage(string parentPageId)
        {
            var page = new Mk20PageSettings
            {
                Id = System.Guid.NewGuid().ToString("N"),
                ParentPageId = parentPageId,
                LeftEncoder = new Mk20EncoderSettings(),
                RightEncoder = new Mk20EncoderSettings(),
            };

            for (int row = 0; row < 4; row++)
            {
                for (int column = 0; column < 5; column++)
                {
                    page.Keys.Add(new Mk20KeySettings { Row = row, Column = column });
                }
            }

            // Folders return from the bottom-right cell, as real vendor themes do.
            if (!string.IsNullOrEmpty(parentPageId))
            {
                Mk20KeySettings back = page.Keys[page.Keys.Count - 1];
                back.ActionType = KeyActionKinds.OneLevelUp;
                back.Title = "BACK";
            }

            return page;
        }
    }

    /// <summary>
    /// One theme page. A page with <see cref="ParentPageId"/> set is a folder; the
    /// device requires that link for a "one level up" key to return correctly.
    /// Encoders are configured per page, as the device stores them per page.
    /// </summary>
    public sealed class Mk20PageSettings
    {
        public string Id { get; set; }

        public string ParentPageId { get; set; }

        /// <summary>Background drawn on the main screen, if any.</summary>
        public string BackgroundPath { get; set; }

        /// <summary>Background drawn on the secondary screen, if any.</summary>
        public string SecondaryBackgroundPath { get; set; }

        /// <summary>
        /// Which part of the source survives the crop when its shape does not match the
        /// 428x142 strip. -1 is hard left, 0 centred, +1 hard right.
        /// </summary>
        public double SecondaryBackgroundOffsetX { get; set; }

        /// <summary>-1 is hard top, 0 centred, +1 hard bottom.</summary>
        public double SecondaryBackgroundOffsetY { get; set; }

        /// <summary>
        /// Show the whole picture, padded to the strip, instead of cropping it to
        /// fill. The device keeps the original proportions, so this is the default.
        /// </summary>
        public bool SecondaryBackgroundFit { get; set; } = true;

        public Mk20EncoderSettings LeftEncoder { get; set; } = new Mk20EncoderSettings();

        public Mk20EncoderSettings RightEncoder { get; set; } = new Mk20EncoderSettings();

        public List<Mk20KeySettings> Keys { get; set; } = new List<Mk20KeySettings>();

        /// <summary>Widgets drawn on the secondary screen.</summary>
        public List<Mk20WidgetSettings> Widgets { get; set; } = new List<Mk20WidgetSettings>();
    }

    /// <summary>One key on a page, addressed the way the device addresses it.</summary>
    public sealed class Mk20KeySettings
    {
        public int Row { get; set; }

        public int Column { get; set; }

        /// <summary>Text drawn over the icon.</summary>
        public string Title { get; set; }

        /// <summary>Title size in points. Vendor themes use 18-24.</summary>
        public double TitleFontSize { get; set; } = KeyTitleDefaults.FontSize;

        /// <summary>Title colour as #rrggbb.</summary>
        public string TitleColor { get; set; } = KeyTitleDefaults.Color;

        /// <summary>Only "top" and "bottom" render on the device.</summary>
        public string TitlePosition { get; set; } = KeyTitleDefaults.Position;

        /// <summary>Picture or GIF used as the key's icon.</summary>
        public string MediaPath { get; set; }

        /// <summary>Keep the icon's alpha so a page background shows through.</summary>
        public bool PreserveAlpha { get; set; } = true;

        /// <summary>
        /// Show the whole picture on the key, padded to the square, instead of
        /// cropping it to fill. The device pads by default, so this matches it.
        /// </summary>
        public bool IconFit { get; set; } = true;

        /// <summary>Crop pan when <see cref="IconFit"/> is off. -1 left, +1 right.</summary>
        public double IconOffsetX { get; set; }

        /// <summary>Crop pan when <see cref="IconFit"/> is off. -1 top, +1 bottom.</summary>
        public double IconOffsetY { get; set; }

        public string ActionType { get; set; } = KeyActionKinds.Unassigned;

        /// <summary>Target page for <see cref="KeyActionKinds.OpenFolder"/>.</summary>
        public string TargetPageId { get; set; }

        /// <summary>
        /// SimHub action or input name, or the <c>HidKey</c> name for a keyboard key.
        /// </summary>
        public string ActionTarget { get; set; }

        /// <summary>
        /// Id the device reports on press for host-routed keys. Stable per key, so a
        /// binding survives moving the key to another cell or page.
        /// </summary>
        public string CommandId { get; set; }

        /// <summary>Keystroke the device sends for <see cref="KeyActionKinds.KeyboardKey"/>.</summary>
        public Mk20KeystrokeSettings Keystroke { get; set; } = new Mk20KeystrokeSettings();

        /// <summary>Steps the plugin replays for <see cref="KeyActionKinds.Macro"/>.</summary>
        public List<Mk20MacroStepSettings> MacroSteps { get; set; } = new List<Mk20MacroStepSettings>();

        /// <summary>
        /// Takes on another key's appearance and action.
        ///
        /// Row and column are left alone because they say where this key is rather
        /// than what it does. The command id is minted afresh: the router keys every
        /// host-routed press by it, so two keys sharing one would make the copy
        /// answer for the original. A folder likewise belongs to the single key that
        /// opens it, so the copy starts without one and the editor gives it its own.
        /// </summary>
        public void ApplyFrom(Mk20KeySettings source)
        {
            if (source == null)
            {
                return;
            }

            Title = source.Title;
            TitleFontSize = source.TitleFontSize;
            TitleColor = source.TitleColor;
            TitlePosition = source.TitlePosition;

            MediaPath = source.MediaPath;
            PreserveAlpha = source.PreserveAlpha;
            IconFit = source.IconFit;
            IconOffsetX = source.IconOffsetX;
            IconOffsetY = source.IconOffsetY;

            ActionType = source.ActionType;
            ActionTarget = source.ActionTarget;
            TargetPageId = null;

            Keystroke = source.Keystroke == null
                ? new Mk20KeystrokeSettings()
                : source.Keystroke.Clone();

            MacroSteps = CloneSteps(source.MacroSteps);

            CommandId = null;
            ThemeComposer.CommandIdFor(this);
        }

        /// <summary>Returns the key to how it starts life: blank, bare and unassigned.</summary>
        public void Reset()
        {
            Title = null;
            TitleFontSize = KeyTitleDefaults.FontSize;
            TitleColor = KeyTitleDefaults.Color;
            TitlePosition = KeyTitleDefaults.Position;

            MediaPath = null;
            PreserveAlpha = true;
            IconFit = true;
            IconOffsetX = 0;
            IconOffsetY = 0;

            ActionType = KeyActionKinds.Unassigned;
            ActionTarget = null;
            TargetPageId = null;
            CommandId = null;

            Keystroke = new Mk20KeystrokeSettings();
            MacroSteps = new List<Mk20MacroStepSettings>();
        }

        /// <summary>A detached copy, so the clipboard cannot be edited from the grid.</summary>
        public Mk20KeySettings Snapshot()
        {
            var copy = new Mk20KeySettings { Row = Row, Column = Column };
            copy.ApplyFrom(this);
            return copy;
        }

        /// <summary>
        /// Steps are cloned one by one rather than serialized, so the settings model
        /// stays free of any dependency beyond itself.
        /// </summary>
        private static List<Mk20MacroStepSettings> CloneSteps(List<Mk20MacroStepSettings> steps)
        {
            var copies = new List<Mk20MacroStepSettings>();

            if (steps == null)
            {
                return copies;
            }

            foreach (Mk20MacroStepSettings step in steps)
            {
                if (step != null)
                {
                    copies.Add(step.Clone());
                }
            }

            return copies;
        }
    }

    /// <summary>
    /// Defaults matching the vendor themes, so an untouched key looks native. Only
    /// "top" and "bottom" are real alignments; the device ignores anything else.
    /// </summary>
    public static class KeyTitleDefaults
    {
        public const double FontSize = 20;
        public const string Color = "#ffffff";
        public const string Position = "bottom";

        public static readonly double[] FontSizes = { 12, 14, 16, 18, 20, 24, 28, 32 };

        public static readonly string[] Positions = { "bottom", "top" };

        /// <summary>Named colours offered in the picker, matching the icon templates.</summary>
        public static readonly KeyValuePair<string, string>[] Colors =
        {
            new KeyValuePair<string, string>("White", "#ffffff"),
            new KeyValuePair<string, string>("Cyan", "#24c8ee"),
            new KeyValuePair<string, string>("Blue", "#379cee"),
            new KeyValuePair<string, string>("Indigo", "#5f7fee"),
            new KeyValuePair<string, string>("Violet", "#9d71ee"),
            new KeyValuePair<string, string>("Purple", "#c64dee"),
            new KeyValuePair<string, string>("Teal", "#2ed1c4"),
            new KeyValuePair<string, string>("Emerald", "#17d6a5"),
            new KeyValuePair<string, string>("Green", "#5cd464"),
            new KeyValuePair<string, string>("Lime", "#9cd446"),
            new KeyValuePair<string, string>("Yellow", "#edd146"),
            new KeyValuePair<string, string>("Orange", "#ed6a35"),
            new KeyValuePair<string, string>("Coral", "#ed8362"),
            new KeyValuePair<string, string>("Red", "#e04141"),
            new KeyValuePair<string, string>("Grey", "#9aa0a6"),
            new KeyValuePair<string, string>("Black", "#000000"),
        };
    }

    /// <summary>Action kinds a key can carry. Persisted verbatim, so keep the values stable.</summary>
    public static class KeyActionKinds
    {
        public const string Unassigned = "Unassigned";
        public const string KeyboardKey = "Keystroke";
        public const string Macro = "Macro";
        public const string SimHubAction = "SimHub action";
        public const string SimHubInput = "SimHub input";
        public const string OpenFolder = "Open folder";
        public const string OneLevelUp = "One level up";
        public const string PreviousPage = "Previous page";
        public const string NextPage = "Next page";

        public static readonly string[] All =
        {
            Unassigned,
            KeyboardKey,
            Macro,
            SimHubAction,
            SimHubInput,
            OpenFolder,
            OneLevelUp,
            PreviousPage,
            NextPage,
        };

        /// <summary>True when the device performs the action itself rather than reporting it.</summary>
        public static bool IsNavigation(string actionType)
        {
            return actionType == OpenFolder
                || actionType == OneLevelUp
                || actionType == PreviousPage
                || actionType == NextPage;
        }

        /// <summary>
        /// True when the device only reports the press and the plugin does the work.
        /// These stop working while SimHub is closed.
        /// </summary>
        public static bool IsHostRouted(string actionType)
        {
            return actionType == Macro
                || actionType == SimHubAction
                || actionType == SimHubInput;
        }

        /// <summary>True when the device acts entirely on its own.</summary>
        public static bool RunsOnDevice(string actionType)
        {
            return actionType == KeyboardKey || IsNavigation(actionType);
        }

        public static string GlyphFor(string actionType)
        {
            switch (actionType)
            {
                case OpenFolder: return "\u25A6";
                case OneLevelUp: return "\u21B0";
                case PreviousPage: return "\u25C0";
                case NextPage: return "\u25B6";
                default: return null;
            }
        }
    }
}

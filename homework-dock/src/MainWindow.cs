using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;
using Path = System.Windows.Shapes.Path;
using WpfCalendar = System.Windows.Controls.Calendar;

namespace HomeworkDock
{
    public class MainWindow : Window
    {
        private const double ShadowMargin = 18;
        private const double PanelWidth = 436;
        private const double WindowWidth = PanelWidth + ShadowMargin * 2;
        private const double CompactHeight = 430;
        private const double DetailHeight = 760;
        private const double MinWindowHeight = 420;
        private const double ExpandedComposerHeight = 520;
        private const double ResizeGripHeight = 12;
        private const string VersionText = "1.1";

        private readonly StateStore _store;
        private readonly AppState _state;
        private readonly bool _startupLaunch;
        private readonly bool _noAutostart;

        private bool _allowClose;
        private bool _exiting;
        private bool _balloonShown;
        private bool _settingsOpenAtMouseDown;
        private bool _thumbDragging;
        private double _thumbStartY;
        private double _thumbStartOffset;
        private bool _focusMode;
        private bool _resizeDragging;
        private double _resizeStartHeight;
        private double _resizeStartY;
        private double _heightBeforeDetail;
        private double _heightBeforeComposer;
        private string _filter = "all";
        private int _addPriority = Priority.Medium;
        private DateTime? _addDue;
        private string _todayKey;
        private string _autostartError;

        private Grid _root;
        private Border _panel;
        private Grid _content;
        private Grid _overlayHost;
        private StackPanel _headerLeft;
        private TextBlock _dateText;
        private TextBlock _subtitleText;
        private Button _pinButton;
        private Path _pinIcon;
        private Button _settingsButton;
        private Button _hideButton;
        private Button _closeButton;
        private Grid _composerHost;
        private Button _composerCollapsedBar;
        private Border _filterRow;
        private Grid _listHost;
        private Border _footer;
        private Grid _focusHost;
        private ScrollViewer _focusScroll;
        private StackPanel _focusPanel;
        private Border _resizeGrip;
        private Border _addCard;
        private TextBox _addBox;
        private TextBlock _addPlaceholder;
        private StackPanel _addPriorityPanel;
        private Button _addDueButton;
        private TextBlock _addDueText;
        private Path _addDueIcon;
        private ScrollViewer _scroll;
        private StackPanel _listPanel;
        private Grid _scrollTrack;
        private Border _scrollThumb;
        private StackPanel _filterPanel;
        private Grid _progressGrid;
        private ColumnDefinition _barFill;
        private ColumnDefinition _barRest;
        private TextBlock _progressText;
        private TextBlock _deliveredText;
        private Border _toast;
        private TextBlock _toastText;
        private Button _toastUndo;
        private DispatcherTimer _toastTimer;
        private int _toastVersion;
        private Action _undoAction;
        private Popup _settingsPopup;
        private Popup _duePopup;
        private TrayIcon _tray;
        private DispatcherTimer _dayTimer;
        private readonly Dictionary<string, Border> _cards = new Dictionary<string, Border>();

        public MainWindow(StateStore store, AppState state, bool startupLaunch, bool noAutostart)
        {
            _store = store;
            _state = state;
            _startupLaunch = startupLaunch;
            _noAutostart = noAutostart;
            _todayKey = DateUtil.TodayKey();
            _focusMode = _state.FocusMode;

            ConfigureWindow();
            BuildShell();
            ApplyViewMode();
            ApplyComposerState(false);
            SetupInitialPosition();
            Topmost = _state.Settings.Topmost;
            InitializeAutostart();
            SetupTray();
            RefreshUI(false, null);

            Loaded += OnWindowLoaded;
            Closing += OnWindowClosing;
            Closed += OnWindowClosed;
            PreviewKeyDown += OnWindowPreviewKeyDown;

            _dayTimer = new DispatcherTimer();
            _dayTimer.Interval = TimeSpan.FromMinutes(1);
            _dayTimer.Tick += OnDayTimerTick;
            _dayTimer.Start();
        }

        public TrayIcon Tray
        {
            get { return _tray; }
        }

        private static Duration Ms(double milliseconds)
        {
            return new Duration(TimeSpan.FromMilliseconds(milliseconds));
        }

        private static QuadraticEase EaseOut()
        {
            QuadraticEase ease = new QuadraticEase();
            ease.EasingMode = EasingMode.EaseOut;
            return ease;
        }

        private void ConfigureWindow()
        {
            Title = "作业清单";
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = true;
            WindowStartupLocation = WindowStartupLocation.Manual;
            Width = WindowWidth;

            Height = RequestedWindowHeight();
            if (_focusMode && _state != null && _state.ComposerExpanded)
            {
                _heightBeforeComposer = _state.WindowHeight > 0 ? _state.WindowHeight : CompactHeight;
            }

            SnapsToDevicePixels = true;
            UseLayoutRounding = true;
            FontFamily = UI.FontUI;
            TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
            Icon = AppIcon.Load();
            Opacity = 0;
        }

        private double RequestedWindowHeight()
        {
            double wanted = _state != null && _state.WindowHeight > 0
                ? _state.WindowHeight
                : (_focusMode ? CompactHeight : DetailHeight);
            if (_focusMode && _state != null && _state.ComposerExpanded)
            {
                wanted = Math.Max(wanted, ExpandedComposerHeight);
            }
            return ClampWindowHeight(wanted);
        }

        private double ClampWindowHeight(double value)
        {
            Rect work = SystemParameters.WorkArea;
            double max = Math.Max(MinWindowHeight, work.Height - 30);
            if (!double.IsNaN(Top) && !double.IsInfinity(Top) && Top > work.Top)
            {
                max = Math.Min(max, work.Bottom - Top - 8);
                max = Math.Max(MinWindowHeight, max);
            }
            if (double.IsNaN(value) || double.IsInfinity(value)) value = _focusMode ? CompactHeight : DetailHeight;
            return Math.Min(max, Math.Max(MinWindowHeight, value));
        }

        private void BuildShell()
        {
            _panel = new Border();
            _panel.Margin = new Thickness(ShadowMargin);
            _panel.CornerRadius = new CornerRadius(26);
            _panel.BorderThickness = new Thickness(1);
            _panel.BorderBrush = UI.Alpha(Color.FromRgb(0xFF, 0xFF, 0xFF), 0xD0);
            _panel.ClipToBounds = true;

            LinearGradientBrush background = new LinearGradientBrush();
            background.StartPoint = new Point(0.15, 0);
            background.EndPoint = new Point(0.85, 1);
            background.GradientStops.Add(new GradientStop(Palette.BackgroundTop, 0));
            background.GradientStops.Add(new GradientStop(Palette.BackgroundBottom, 1));
            background.Freeze();
            _panel.Background = background;
            _panel.Effect = UI.Shadow(36, 10, 0.20);

            Grid host = new Grid();
            host.Children.Add(BuildDecoration());

            _content = new Grid();
            _content.Margin = new Thickness(22, 18, 22, 14);
            _content.RowDefinitions.Add(UI.AutoRow());
            _content.RowDefinitions.Add(UI.AutoRow());
            _content.RowDefinitions.Add(UI.AutoRow());
            _content.RowDefinitions.Add(UI.StarRow());
            _content.RowDefinitions.Add(UI.AutoRow());
            Panel.SetZIndex(_content, 1);
            host.Children.Add(_content);

            _content.Children.Add(BuildHeader());

            _composerHost = BuildComposerHost();
            Grid.SetRow(_composerHost, 1);
            _content.Children.Add(_composerHost);

            _filterRow = BuildFilterRowContainer();
            Grid.SetRow(_filterRow, 2);
            _content.Children.Add(_filterRow);

            _listHost = BuildList();
            Grid.SetRow(_listHost, 3);
            _content.Children.Add(_listHost);

            _focusHost = BuildFocusHost();
            Grid.SetRow(_focusHost, 3);
            _content.Children.Add(_focusHost);

            _footer = BuildFooter();
            Grid.SetRow(_footer, 4);
            _content.Children.Add(_footer);

            _toast = BuildToast();
            Panel.SetZIndex(_toast, 5);
            host.Children.Add(_toast);

            _panel.Child = host;

            _root = new Grid();
            _root.Children.Add(_panel);

            _overlayHost = new Grid();
            _overlayHost.Margin = new Thickness(ShadowMargin);
            _overlayHost.Visibility = Visibility.Collapsed;
            Panel.SetZIndex(_overlayHost, 100);
            _root.Children.Add(_overlayHost);

            _resizeGrip = BuildResizeGrip();
            Panel.SetZIndex(_resizeGrip, 50);
            _root.Children.Add(_resizeGrip);

            Content = _root;
        }

        private UIElement BuildDecoration()
        {
            Grid decor = new Grid();
            decor.IsHitTestVisible = false;

            Ellipse glow = new Ellipse();
            glow.Width = 390;
            glow.Height = 390;
            glow.HorizontalAlignment = HorizontalAlignment.Right;
            glow.VerticalAlignment = VerticalAlignment.Top;
            glow.Margin = new Thickness(0, -190, -150, 0);
            RadialGradientBrush coral = new RadialGradientBrush();
            coral.GradientStops.Add(new GradientStop(Color.FromArgb(0x34, 0xD9, 0x58, 0x3B), 0));
            coral.GradientStops.Add(new GradientStop(Color.FromArgb(0x0C, 0xD9, 0x58, 0x3B), 0.55));
            coral.GradientStops.Add(new GradientStop(Color.FromArgb(0x00, 0xD9, 0x58, 0x3B), 1));
            coral.Freeze();
            glow.Fill = coral;
            decor.Children.Add(glow);

            Ellipse sage = new Ellipse();
            sage.Width = 300;
            sage.Height = 300;
            sage.HorizontalAlignment = HorizontalAlignment.Left;
            sage.VerticalAlignment = VerticalAlignment.Bottom;
            sage.Margin = new Thickness(-170, 0, 0, -175);
            RadialGradientBrush green = new RadialGradientBrush();
            green.GradientStops.Add(new GradientStop(Color.FromArgb(0x1E, 0x4D, 0x72, 0x66), 0));
            green.GradientStops.Add(new GradientStop(Color.FromArgb(0x00, 0x4D, 0x72, 0x66), 1));
            green.Freeze();
            sage.Fill = green;
            decor.Children.Add(sage);

            Border hairline = new Border();
            hairline.Height = 1;
            hairline.VerticalAlignment = VerticalAlignment.Top;
            hairline.Margin = new Thickness(46, 9, 46, 0);
            hairline.Background = UI.Alpha(Color.FromRgb(0xFF, 0xFF, 0xFF), 0xB0);
            decor.Children.Add(hairline);

            return decor;
        }

        private UIElement BuildHeader()
        {
            Grid header = new Grid();
            header.Margin = new Thickness(0, 0, 0, 14);
            header.ColumnDefinitions.Add(UI.Star());
            header.ColumnDefinitions.Add(UI.Auto());

            _headerLeft = new StackPanel();
            _headerLeft.Cursor = Cursors.SizeAll;
            _headerLeft.Background = Brushes.Transparent;
            _headerLeft.MouseLeftButtonDown += HeaderMouseDown;

            _dateText = UI.Text("", 11.5, B(Palette.Muted), FontWeights.SemiBold);
            _dateText.ToolTip = "按住这里拖动窗口";
            _headerLeft.Children.Add(_dateText);

            Grid titleRow = new Grid();
            titleRow.Margin = new Thickness(0, 2, 0, 0);
            titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            titleRow.ColumnDefinitions.Add(UI.Star());

            Border accent = new Border();
            accent.Width = 4;
            accent.Height = 22;
            accent.CornerRadius = new CornerRadius(2);
            accent.Background = B(Palette.Accent);
            accent.VerticalAlignment = VerticalAlignment.Center;
            accent.Margin = new Thickness(0, 0, 9, 0);
            titleRow.Children.Add(accent);

            TextBlock title = UI.Text("作业清单", 26, B(Palette.Ink), FontWeights.Bold);
            title.FontFamily = new FontFamily("Microsoft YaHei UI, Microsoft YaHei, Segoe UI");
            Grid.SetColumn(title, 1);
            titleRow.Children.Add(title);
            _headerLeft.Children.Add(titleRow);

            _subtitleText = UI.Text("", 11.5, B(Palette.Muted));
            _subtitleText.Margin = new Thickness(13, 3, 0, 0);
            _headerLeft.Children.Add(_subtitleText);

            Grid.SetColumn(_headerLeft, 0);
            header.Children.Add(_headerLeft);

            StackPanel actions = new StackPanel();
            actions.Orientation = Orientation.Horizontal;
            actions.VerticalAlignment = VerticalAlignment.Top;
            actions.Margin = new Thickness(0, 3, 0, 0);

            Path pinIcon;
            _pinButton = BuildHeaderButton(
                "M9 3h6l-1 5 3 3v2h-4v6l-1 1-1-1v-6H7v-2l3-3z",
                "窗口置顶", "PinButton", ToggleTopmost, out pinIcon);
            _pinIcon = pinIcon;
            _pinButton.MouseEnter += delegate
            {
                _pinButton.Background = _state.Settings.Topmost ? B("#F5D8CD") : B("#F2EBE0");
            };
            _pinButton.MouseLeave += delegate { UpdatePinVisual(); };
            actions.Children.Add(_pinButton);

            Path settingsIcon;
            _settingsButton = BuildHeaderButton(
                "M4 6.5h4.2 M12.2 6.5H20 M4 12h8.2 M16.2 12H20 M4 17.5h2.2 M10.2 17.5H20 M10.5 4.5v4 M14.5 10v4 M8.5 15.5v4",
                "设置", "SettingsButton", ShowSettings, out settingsIcon);
            UI.SetName(_settingsButton, "设置");
            _settingsButton.Margin = new Thickness(2, 0, 0, 0);
            _settingsButton.PreviewMouseLeftButtonDown += delegate
            {
                _settingsOpenAtMouseDown = _settingsPopup != null && _settingsPopup.IsOpen;
            };
            actions.Children.Add(_settingsButton);

            Path hideIcon;
            _hideButton = BuildHeaderButton(
                "M5 10h10", "最小化到托盘", "HideButton", HideToTray, out hideIcon);
            _hideButton.Margin = new Thickness(2, 0, 0, 0);
            actions.Children.Add(_hideButton);

            Path closeIcon;
            _closeButton = BuildHeaderButton(
                "M6 6l8 8 M14 6l-8 8", "退出作业清单", "ExitButton", ExitApp, out closeIcon);
            _closeButton.Margin = new Thickness(2, 0, 0, 0);
            _closeButton.MouseEnter += delegate { _closeButton.Background = B("#FAE8E1"); };
            _closeButton.MouseLeave += delegate { _closeButton.Background = Brushes.Transparent; };
            actions.Children.Add(_closeButton);

            Grid.SetColumn(actions, 1);
            header.Children.Add(actions);

            UpdatePinVisual();
            return header;
        }

        private Button BuildHeaderButton(string path, string name, string automationId,
            Action onClick, out Path icon)
        {
            icon = UI.IconPath(path, B(Palette.Muted), 15, 1.6);
            Button button = UI.FlatButton(icon, 15, onClick);
            button.Width = 30;
            button.Height = 30;
            button.Background = Brushes.Transparent;
            button.ToolTip = name;
            UI.SetName(button, name);
            UI.SetId(button, automationId);
            button.MouseEnter += delegate
            {
                if (button.Background == Brushes.Transparent) button.Background = B("#F2EBE0");
            };
            button.MouseLeave += delegate
            {
                if (button != _pinButton) button.Background = Brushes.Transparent;
            };
            return button;
        }

        private Grid BuildComposerHost()
        {
            Grid host = new Grid();
            _addCard = BuildComposer();
            host.Children.Add(_addCard);
            _composerCollapsedBar = BuildCollapsedComposer();
            host.Children.Add(_composerCollapsedBar);
            return host;
        }

        private Button BuildCollapsedComposer()
        {
            Grid content = new Grid();
            content.Margin = new Thickness(12, 0, 12, 0);
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            content.ColumnDefinitions.Add(UI.Star());
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            Path plus = UI.IconPath("M10 4v12 M4 10h12", B(Palette.Accent), 12, 1.8);
            plus.VerticalAlignment = VerticalAlignment.Center;
            content.Children.Add(plus);

            TextBlock label = UI.Text("添加一项作业", 12.2, B(Palette.Ink), FontWeights.Medium);
            label.Margin = new Thickness(7, 0, 0, 0);
            Grid.SetColumn(label, 1);
            content.Children.Add(label);

            TextBlock hint = UI.Text("展开", 9.8, B(Palette.Faint));
            hint.VerticalAlignment = VerticalAlignment.Center;
            hint.Margin = new Thickness(0, 0, 4, 0);
            Grid.SetColumn(hint, 2);
            content.Children.Add(hint);

            Path chevron = UI.IconPath("M6 8l4 4 4-4", B(Palette.Faint), 11, 1.4);
            chevron.VerticalAlignment = VerticalAlignment.Center;
            chevron.Margin = new Thickness(0, 1, 0, 0);
            Grid.SetColumn(chevron, 3);
            content.Children.Add(chevron);

            Button bar = UI.FlatButton(content, 13, ExpandComposerAndFocus);
            bar.Height = 40;
            bar.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            bar.Background = UI.Alpha(Color.FromRgb(0xFF, 0xFF, 0xFF), 0xB8);
            bar.BorderBrush = B(Palette.Rule);
            bar.BorderThickness = new Thickness(1);
            bar.ToolTip = "展开输入栏";
            UI.SetName(bar, "展开输入栏");
            UI.SetId(bar, "ComposerExpand");
            UI.Hover(bar, UI.Alpha(Color.FromRgb(0xFF, 0xFF, 0xFF), 0xB8), B("#FFFDF8"));
            return bar;
        }

        private Border BuildComposer()
        {
            _addCard = new Border();
            _addCard.Background = UI.Alpha(Color.FromRgb(0xFF, 0xFF, 0xFF), 0xD8);
            _addCard.BorderBrush = B(Palette.RuleStrong);
            _addCard.BorderThickness = new Thickness(1);
            _addCard.CornerRadius = new CornerRadius(16);
            _addCard.Padding = new Thickness(12, 10, 10, 10);
            _addCard.SnapsToDevicePixels = true;

            StackPanel body = new StackPanel();

            Grid inputHost = new Grid();
            _addBox = new TextBox();
            _addBox.FontFamily = UI.FontUI;
            _addBox.FontSize = 15;
            _addBox.Foreground = B(Palette.Ink);
            _addBox.Background = Brushes.Transparent;
            _addBox.BorderThickness = new Thickness(0);
            _addBox.Padding = new Thickness(0);
            _addBox.Margin = new Thickness(0);
            _addBox.Height = 27;
            _addBox.MaxLength = 160;
            _addBox.AcceptsReturn = false;
            _addBox.TextWrapping = TextWrapping.NoWrap;
            _addBox.VerticalContentAlignment = VerticalAlignment.Center;
            _addBox.CaretBrush = B(Palette.Accent);
            _addBox.SelectionBrush = UI.Alpha(Palette.Accent, 0x66);
            _addBox.FocusVisualStyle = null;
            AutomationProperties.SetName(_addBox, "作业内容");
            AutomationProperties.SetAutomationId(_addBox, "AddTaskInput");
            _addBox.TextChanged += delegate { UpdateAddPlaceholder(); };
            _addBox.KeyDown += OnAddBoxKeyDown;
            _addBox.GotKeyboardFocus += delegate { _addCard.BorderBrush = B(Palette.Accent); };
            _addBox.LostKeyboardFocus += delegate
            {
                _addCard.BorderBrush = B(Palette.RuleStrong);
            };
            inputHost.Children.Add(_addBox);

            _addPlaceholder = UI.Text("写下下一项作业…", 14.5, B(Palette.Muted));
            _addPlaceholder.Margin = new Thickness(2, 0, 0, 0);
            _addPlaceholder.IsHitTestVisible = false;
            inputHost.Children.Add(_addPlaceholder);
            body.Children.Add(inputHost);

            Grid tools = new Grid();
            tools.Margin = new Thickness(0, 9, 0, 0);
            tools.ColumnDefinitions.Add(UI.Star());
            tools.ColumnDefinitions.Add(UI.Auto());
            tools.ColumnDefinitions.Add(UI.Auto());

            StackPanel left = new StackPanel();
            left.Orientation = Orientation.Horizontal;
            left.VerticalAlignment = VerticalAlignment.Center;

            TextBlock priorityLabel = UI.Text("轻重缓急", 10.5, B(Palette.Muted));
            priorityLabel.Margin = new Thickness(0, 0, 7, 0);
            left.Children.Add(priorityLabel);

            _addPriorityPanel = new StackPanel();
            _addPriorityPanel.Orientation = Orientation.Horizontal;
            _addPriorityPanel.VerticalAlignment = VerticalAlignment.Center;
            left.Children.Add(_addPriorityPanel);
            RebuildComposerPriorities();

            Border divider = new Border();
            divider.Width = 1;
            divider.Height = 14;
            divider.Background = B(Palette.Rule);
            divider.Margin = new Thickness(9, 0, 9, 0);
            left.Children.Add(divider);

            _addDueIcon = UI.IconPath("M4 6h12v10H4z M4 9h12 M7 4v3 M13 4v3",
                B(Palette.Muted), 12, 1.4);
            _addDueText = UI.Text("截止日期", 10.8, B(Palette.Muted), FontWeights.Medium);
            _addDueText.Margin = new Thickness(4, 0, 0, 0);
            StackPanel dueContent = new StackPanel();
            dueContent.Orientation = Orientation.Horizontal;
            dueContent.VerticalAlignment = VerticalAlignment.Center;
            dueContent.Children.Add(_addDueIcon);
            dueContent.Children.Add(_addDueText);

            _addDueButton = UI.FlatButton(dueContent, 8, ShowComposerDuePicker);
            _addDueButton.Height = 23;
            _addDueButton.Padding = new Thickness(7, 0, 7, 0);
            _addDueButton.BorderThickness = new Thickness(1);
            _addDueButton.BorderBrush = B(Palette.Rule);
            _addDueButton.Background = Brushes.Transparent;
            _addDueButton.ToolTip = "设置截止日期";
            UI.SetName(_addDueButton, "设置截止日期");
            UI.SetId(_addDueButton, "ComposerDueButton");
            _addDueButton.MouseEnter += delegate
            {
                _addDueButton.Background = _addDue.HasValue ? B("#F6DBD1") : B("#F5EFE4");
            };
            _addDueButton.MouseLeave += delegate { UpdateComposerDueVisual(); };
            left.Children.Add(_addDueButton);

            Grid.SetColumn(left, 0);
            tools.Children.Add(left);

            Button collapse = UI.FlatButton(
                UI.IconPath("M5 12l5-5 5 5", B(Palette.Muted), 13, 1.5), 7,
                delegate { SetComposerExpanded(false); });
            collapse.Width = 24;
            collapse.Height = 24;
            collapse.Background = Brushes.Transparent;
            collapse.ToolTip = "收起输入栏";
            UI.SetName(collapse, "收起输入栏");
            UI.SetId(collapse, "ComposerCollapse");
            UI.Hover(collapse, Brushes.Transparent, B("#F2EBE0"));
            collapse.Margin = new Thickness(0, 0, 10, 0);
            Grid.SetColumn(collapse, 1);
            tools.Children.Add(collapse);

            Button addButton = UI.FlatButton(
                UI.IconPath("M10 4v12 M4 10h12", B(Colors.White), 14, 2.2),
                10, AddTaskFromComposer);
            addButton.Width = 30;
            addButton.Height = 30;
            addButton.Background = B(Palette.Accent);
            addButton.ToolTip = "添加作业（Enter）";
            UI.SetName(addButton, "添加作业");
            UI.SetId(addButton, "AddTaskButton");
            UI.Hover(addButton, B(Palette.Accent), B(Palette.AccentDeep));
            Grid.SetColumn(addButton, 2);
            tools.Children.Add(addButton);

            body.Children.Add(tools);
            _addCard.Child = body;
            return _addCard;
        }

        private void ApplyComposerState(bool persist)
        {
            bool expanded = _state != null && _state.ComposerExpanded;
            if (_addCard != null) _addCard.Visibility = expanded ? Visibility.Visible : Visibility.Collapsed;
            if (_composerCollapsedBar != null)
            {
                _composerCollapsedBar.Visibility = expanded ? Visibility.Collapsed : Visibility.Visible;
            }
            if (persist && _state != null)
            {
                _state.ComposerExpanded = expanded;
                Persist();
            }
        }

        private void SetComposerExpanded(bool expanded)
        {
            if (_state == null) return;
            if (_state.ComposerExpanded == expanded)
            {
                ApplyComposerState(false);
                return;
            }
            if (expanded && _focusMode && Height < ExpandedComposerHeight && _heightBeforeComposer <= 0)
            {
                _heightBeforeComposer = Height;
            }
            _state.ComposerExpanded = expanded;
            ApplyComposerState(false);
            if (expanded && _focusMode && Height < ExpandedComposerHeight)
            {
                Height = ClampWindowHeight(ExpandedComposerHeight);
            }
            if (!expanded)
            {
                if (_focusMode && _heightBeforeComposer > 0)
                {
                    Height = ClampWindowHeight(_heightBeforeComposer);
                }
                _heightBeforeComposer = 0;
            }
            _state.WindowHeight = Height;
            Persist();
        }

        private void ExpandComposerAndFocus()
        {
            SetComposerExpanded(true);
            Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(FocusComposer));
        }

        private void UpdateAddPlaceholder()
        {
            if (_addPlaceholder == null || _addBox == null) return;
            _addPlaceholder.Visibility = string.IsNullOrEmpty(_addBox.Text)
                ? Visibility.Visible : Visibility.Collapsed;
        }

        private void OnAddBoxKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None)
            {
                e.Handled = true;
                AddTaskFromComposer();
            }
        }

        private void RebuildComposerPriorities()
        {
            _addPriorityPanel.Children.Clear();
            int[] order = new int[] { Priority.High, Priority.Medium, Priority.Low };
            for (int i = 0; i < order.Length; i++)
            {
                int value = order[i];
                Button chip = BuildPriorityChip(value, _addPriority == value, false, delegate
                {
                    _addPriority = value;
                    RebuildComposerPriorities();
                }, "Priority_");
                if (i < order.Length - 1) chip.Margin = new Thickness(0, 0, 3, 0);
                _addPriorityPanel.Children.Add(chip);
            }
        }

        private Button BuildPriorityChip(int priority, bool selected, bool wide, Action onClick, string idPrefix)
        {
            Brush color = PriorityBrush(priority);
            Brush soft = PrioritySoftBrush(priority);

            StackPanel content = new StackPanel();
            content.Orientation = Orientation.Horizontal;
            content.VerticalAlignment = VerticalAlignment.Center;

            Ellipse dot = new Ellipse();
            dot.Width = 5;
            dot.Height = 5;
            dot.Fill = selected ? color : B(Palette.Faint);
            dot.Margin = new Thickness(0, 0, 4, 0);
            content.Children.Add(dot);

            TextBlock text = UI.Text(wide ? Priority.LongLabel(priority) : Priority.Label(priority),
                wide ? 11.5 : 11, selected ? color : B(Palette.Muted),
                selected ? FontWeights.SemiBold : FontWeights.Medium);
            content.Children.Add(text);

            Button button = UI.FlatButton(content, 8, onClick);
            button.Height = wide ? 30 : 23;
            if (wide)
            {
                button.Margin = new Thickness(2, 0, 2, 0);
                button.HorizontalAlignment = HorizontalAlignment.Stretch;
                button.HorizontalContentAlignment = HorizontalAlignment.Center;
            }
            else
            {
                button.Width = 34;
                button.HorizontalContentAlignment = HorizontalAlignment.Center;
            }
            button.Padding = new Thickness(wide ? 8 : 4, 0, wide ? 8 : 4, 0);
            button.Background = selected ? soft : B("#F8F3E9");
            button.BorderBrush = selected ? UI.Alpha(((SolidColorBrush)color).Color, 0x66) : Brushes.Transparent;
            button.BorderThickness = new Thickness(selected ? 1 : 0);
            button.ToolTip = Priority.LongLabel(priority);
            UI.SetName(button, Priority.LongLabel(priority) + (selected ? "，已选择" : ""));
            UI.SetId(button, idPrefix + priority.ToString(CultureInfo.InvariantCulture));

            Brush normal = selected ? soft : B("#F8F3E9");
            Brush hover;
            if (priority == Priority.High) hover = B("#F5DCD2");
            else if (priority == Priority.Medium) hover = B("#F7E7C8");
            else hover = B("#DFEBE5");
            UI.Hover(button, normal, hover);
            return button;
        }

        private Brush PriorityBrush(int priority)
        {
            if (priority == Priority.High) return B(Palette.Accent);
            if (priority == Priority.Medium) return B(Palette.Amber);
            return B(Palette.Low);
        }

        private Brush PrioritySoftBrush(int priority)
        {
            if (priority == Priority.High) return B(Palette.AccentSoft);
            if (priority == Priority.Medium) return B(Palette.AmberSoft);
            return B(Palette.LowSoft);
        }

        private void ShowComposerDuePicker()
        {
            ShowDuePicker(_addDueButton, _addDue, delegate(DateTime? date)
            {
                _addDue = date;
                UpdateComposerDueVisual();
            });
        }

        private void UpdateComposerDueVisual()
        {
            if (_addDueButton == null || _addDueText == null) return;
            if (_addDue.HasValue)
            {
                _addDueText.Text = DateUtil.ShortDate(_addDue);
                _addDueText.Foreground = B(Palette.AccentDeep);
                _addDueIcon.Stroke = B(Palette.Accent);
                _addDueButton.Background = B(Palette.AccentSoft);
                _addDueButton.BorderBrush = UI.Alpha(Palette.Accent, 0x55);
                _addDueButton.ToolTip = "截止日期：" + DateUtil.DueLabel(_addDue);
            }
            else
            {
                _addDueText.Text = "截止日期";
                _addDueText.Foreground = B(Palette.Muted);
                _addDueIcon.Stroke = B(Palette.Muted);
                _addDueButton.Background = Brushes.Transparent;
                _addDueButton.BorderBrush = B(Palette.Rule);
                _addDueButton.ToolTip = "设置截止日期";
            }
        }

        private void AddTaskFromComposer()
        {
            if (_overlayHost != null && _overlayHost.Visibility == Visibility.Visible) return;
            string title = _addBox.Text == null ? "" : _addBox.Text.Trim();
            if (title.Length == 0)
            {
                _addCard.BorderBrush = B(Palette.Accent);
                _addBox.Focus();
                DispatcherTimer timer = new DispatcherTimer();
                timer.Interval = TimeSpan.FromMilliseconds(900);
                timer.Tick += delegate
                {
                    timer.Stop();
                    if (!_addBox.IsKeyboardFocusWithin) _addCard.BorderBrush = B(Palette.RuleStrong);
                };
                timer.Start();
                return;
            }

            TaskItem task = TaskItem.Create(title, _addPriority, _addDue);
            _state.Tasks.Add(task);
            _filter = "all";

            _addBox.Text = "";
            _addPriority = Priority.Medium;
            _addDue = null;
            RebuildComposerPriorities();
            UpdateComposerDueVisual();
            UpdateAddPlaceholder();

            Persist();
            RefreshUI(false, task.Id);
            HighlightCard(task.Id);
            _addBox.Focus();
        }

        private Border BuildFilterRowContainer()
        {
            Border container = new Border();
            container.Margin = new Thickness(0, 12, 0, 8);

            Grid row = new Grid();
            row.ColumnDefinitions.Add(UI.Star());
            row.ColumnDefinitions.Add(UI.Auto());

            _filterPanel = new StackPanel();
            _filterPanel.Orientation = Orientation.Horizontal;
            _filterPanel.VerticalAlignment = VerticalAlignment.Center;
            row.Children.Add(_filterPanel);

            StackPanel detailActions = new StackPanel();
            detailActions.Orientation = Orientation.Horizontal;
            detailActions.HorizontalAlignment = HorizontalAlignment.Right;
            detailActions.VerticalAlignment = VerticalAlignment.Center;

            StackPanel focusContent = new StackPanel();
            focusContent.Orientation = Orientation.Horizontal;
            focusContent.VerticalAlignment = VerticalAlignment.Center;
            focusContent.Children.Add(UI.Text("回到焦点", 9.8, B(Palette.InkSoft), FontWeights.Medium));
            Button backToFocus = UI.FlatButton(focusContent, 7, delegate { SetFocusMode(true); });
            backToFocus.Height = 24;
            backToFocus.Padding = new Thickness(7, 0, 7, 0);
            backToFocus.Background = Brushes.Transparent;
            backToFocus.ToolTip = "只显示最要紧的一项";
            UI.SetName(backToFocus, "回到焦点模式");
            UI.SetId(backToFocus, "FocusCollapseButton");
            UI.Hover(backToFocus, Brushes.Transparent, B("#F2EBE0"));
            detailActions.Children.Add(backToFocus);

            Grid.SetColumn(detailActions, 1);
            row.Children.Add(detailActions);

            container.Child = row;
            RebuildFilters();
            return container;
        }

        private void RebuildFilters()
        {
            if (_filterPanel == null) return;
            _filterPanel.Children.Clear();
            _filterPanel.Children.Add(BuildFilterButton("all", "全部", _state.Tasks.Count));
            _filterPanel.Children.Add(BuildFilterButton("high", "高优先", _state.HighCount));
            _filterPanel.Children.Add(BuildFilterButton("done", "待交付", _state.DoneCount));
        }

        private Button BuildFilterButton(string key, string label, int count)
        {
            bool selected = string.Equals(_filter, key, StringComparison.Ordinal);
            StackPanel content = new StackPanel();
            content.Orientation = Orientation.Horizontal;
            content.VerticalAlignment = VerticalAlignment.Center;

            TextBlock text = UI.Text(label, 11.3,
                selected ? B("#FFF9F0") : B(Palette.InkSoft),
                selected ? FontWeights.SemiBold : FontWeights.Medium);
            content.Children.Add(text);

            TextBlock number = UI.Text(count.ToString(CultureInfo.InvariantCulture), 10,
                selected ? UI.Alpha(Colors.White, 0xC0) : B(Palette.Faint),
                FontWeights.Medium);
            number.FontFamily = UI.FontNumeric;
            number.Margin = new Thickness(4, 1, 0, 0);
            content.Children.Add(number);

            Button button = UI.FlatButton(content, 9, delegate
            {
                if (_filter == key) return;
                _filter = key;
                RefreshUI(false, null);
            });
            button.Height = 27;
            button.Padding = new Thickness(9, 0, 9, 0);
            button.Margin = new Thickness(0, 0, 4, 0);
            Brush normal = selected ? B(Palette.Ink) : Brushes.Transparent;
            Brush hover = selected ? B(Palette.Ink) : B("#F2EBE0");
            button.Background = normal;
            UI.Hover(button, normal, hover);
            button.ToolTip = key == "high" ? "只看高优先作业" : (key == "done" ? "只看已经做完、等待交付的作业" : "查看全部作业");
            UI.SetName(button, label + "，" + count.ToString(CultureInfo.InvariantCulture) + " 项");
            UI.SetId(button, "Filter_" + key);
            return button;
        }

        private Grid BuildList()
        {
            _scroll = new ScrollViewer();
            _scroll.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;
            _scroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
            _scroll.CanContentScroll = false;
            _scroll.PanningMode = PanningMode.VerticalOnly;
            _scroll.PreviewMouseWheel += OnListMouseWheel;
            _scroll.Focusable = false;
            _scroll.ScrollChanged += delegate { UpdateScrollThumb(); };
            _scroll.SizeChanged += delegate { UpdateScrollThumb(); };

            _listPanel = new StackPanel();
            _listPanel.Margin = new Thickness(0, 0, 6, 0);
            _scroll.Content = _listPanel;

            Grid host = new Grid();
            host.ColumnDefinitions.Add(UI.Star());
            host.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            host.Children.Add(_scroll);

            _scrollTrack = new Grid();
            _scrollTrack.Width = 10;
            _scrollTrack.Margin = new Thickness(4, 0, 0, 0);
            _scrollTrack.Background = Brushes.Transparent;
            _scrollTrack.Cursor = Cursors.Arrow;
            _scrollTrack.MouseLeftButtonDown += OnScrollTrackClick;
            Grid.SetColumn(_scrollTrack, 1);
            host.Children.Add(_scrollTrack);

            _scrollThumb = new Border();
            _scrollThumb.Width = 4;
            _scrollThumb.CornerRadius = new CornerRadius(2);
            _scrollThumb.Background = UI.Alpha(Palette.Ink, 0x28);
            _scrollThumb.HorizontalAlignment = HorizontalAlignment.Right;
            _scrollThumb.VerticalAlignment = VerticalAlignment.Top;
            _scrollThumb.Visibility = Visibility.Collapsed;
            _scrollThumb.Cursor = Cursors.SizeNS;
            _scrollThumb.MouseLeftButtonDown += OnThumbMouseDown;
            _scrollThumb.MouseMove += OnThumbMouseMove;
            _scrollThumb.MouseLeftButtonUp += OnThumbMouseUp;
            _scrollThumb.LostMouseCapture += delegate { _thumbDragging = false; };
            _scrollThumb.MouseEnter += delegate { _scrollThumb.Background = UI.Alpha(Palette.Ink, 0x48); };
            _scrollThumb.MouseLeave += delegate
            {
                if (!_thumbDragging) _scrollThumb.Background = UI.Alpha(Palette.Ink, 0x28);
            };
            _scrollTrack.Children.Add(_scrollThumb);

            return host;
        }

        private Grid BuildFocusHost()
        {
            _focusScroll = new ScrollViewer();
            _focusScroll.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;
            _focusScroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
            _focusScroll.CanContentScroll = false;
            _focusScroll.PanningMode = PanningMode.VerticalOnly;
            _focusScroll.Focusable = false;
            _focusScroll.PreviewMouseWheel += OnFocusMouseWheel;

            _focusPanel = new StackPanel();
            _focusPanel.Margin = new Thickness(0, 0, 6, 0);
            _focusScroll.Content = _focusPanel;

            Grid host = new Grid();
            host.Children.Add(_focusScroll);
            return host;
        }

        private void OnFocusMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Delta == 0 || _focusScroll == null) return;
            double target = _focusScroll.VerticalOffset - (e.Delta / 120.0) * 56.0;
            if (target < 0) target = 0;
            if (target > _focusScroll.ScrollableHeight) target = _focusScroll.ScrollableHeight;
            _focusScroll.ScrollToVerticalOffset(target);
            e.Handled = true;
        }

        private Border BuildResizeGrip()
        {
            Border grip = new Border();
            grip.Height = ResizeGripHeight;
            grip.VerticalAlignment = VerticalAlignment.Bottom;
            grip.Background = UI.Alpha(Palette.Ink, 0x01);
            grip.Cursor = Cursors.SizeNS;
            grip.ToolTip = "拖动调整窗口高度";
            UI.SetName(grip, "拖动调整窗口高度");
            UI.SetId(grip, "ResizeGrip");
            grip.MouseLeftButtonDown += OnResizeMouseDown;
            grip.MouseMove += OnResizeMouseMove;
            grip.MouseLeftButtonUp += OnResizeMouseUp;
            grip.LostMouseCapture += delegate
            {
                if (_resizeDragging)
                {
                    _resizeDragging = false;
                    SaveWindowHeight();
                }
            };

            Border handle = UI.Rounded(UI.Alpha(Palette.Ink, 0x38), 2);
            handle.Width = 46;
            handle.Height = 5;
            handle.HorizontalAlignment = HorizontalAlignment.Center;
            handle.VerticalAlignment = VerticalAlignment.Bottom;
            handle.Margin = new Thickness(0, 0, 0, 7);
            grip.Child = handle;
            grip.MouseEnter += delegate { handle.Background = UI.Alpha(Palette.Ink, 0x60); };
            grip.MouseLeave += delegate { handle.Background = UI.Alpha(Palette.Ink, 0x38); };
            return grip;
        }

        private void OnResizeMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left) return;
            e.Handled = true;
            _heightBeforeComposer = 0;
            _resizeDragging = true;
            _resizeStartHeight = Height;
            _resizeStartY = e.GetPosition(this).Y;
            _resizeGrip.CaptureMouse();
        }

        private void OnResizeMouseMove(object sender, MouseEventArgs e)
        {
            if (!_resizeDragging) return;
            double delta = e.GetPosition(this).Y - _resizeStartY;
            Height = ClampWindowHeight(_resizeStartHeight + delta);
            e.Handled = true;
        }

        private void OnResizeMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!_resizeDragging) return;
            _resizeDragging = false;
            _resizeGrip.ReleaseMouseCapture();
            SaveWindowHeight();
        }

        private void SaveWindowHeight()
        {
            if (_state == null || double.IsNaN(Height) || Height <= 0) return;
            _state.WindowHeight = Height;
            SaveWindowPosition();
            Persist();
        }

        private void ApplyViewMode()
        {
            _focusMode = _state.FocusMode;
            if (_filterRow != null) _filterRow.Visibility = _focusMode ? Visibility.Collapsed : Visibility.Visible;
            if (_listHost != null) _listHost.Visibility = _focusMode ? Visibility.Collapsed : Visibility.Visible;
            if (_focusHost != null) _focusHost.Visibility = _focusMode ? Visibility.Visible : Visibility.Collapsed;
            if (_footer != null) _footer.Visibility = _focusMode ? Visibility.Collapsed : Visibility.Visible;
            if (_focusMode) RebuildFocus();
            else RebuildList();
        }

        private void SetFocusMode(bool focus)
        {
            if (_state.FocusMode == focus)
            {
                ApplyViewMode();
                return;
            }
            _state.FocusMode = focus;
            _focusMode = focus;
            _heightBeforeComposer = 0;
            if (!focus)
            {
                if (Height < DetailHeight - 10)
                {
                    _heightBeforeDetail = Height;
                    Height = ClampWindowHeight(DetailHeight);
                }
            }
            else
            {
                double target = _heightBeforeDetail > 0 ? _heightBeforeDetail : CompactHeight;
                _heightBeforeDetail = 0;
                Height = ClampWindowHeight(target);
                if (_state.ComposerExpanded && Height < ExpandedComposerHeight)
                {
                    Height = ClampWindowHeight(ExpandedComposerHeight);
                }
            }
            _state.WindowHeight = Height;
            ApplyViewMode();
            Persist();
            RefreshUI(true, null);
            if (_settingsPopup != null && _settingsPopup.IsOpen)
            {
                _settingsPopup.Child = BuildSettingsPanel();
            }
        }

        private void RebuildFocus()
        {
            if (_focusPanel == null) return;
            _focusPanel.Children.Clear();
            TaskItem top = FocusTask();
            if (top == null)
            {
                _focusPanel.Children.Add(BuildFocusEmpty());
                return;
            }

            _focusPanel.Children.Add(BuildFocusHero(top));

            int pending = _state.PendingCount;
            int done = _state.DoneCount;
            int rest = top.Done ? Math.Max(0, done - 1) : Math.Max(0, pending - 1);
            string label;
            if (top.Done) label = rest > 0 ? "还有 " + rest.ToString(CultureInfo.InvariantCulture) + " 项待交付" : "最后一件事，交付后就清空了";
            else label = rest > 0 ? "还有 " + rest.ToString(CultureInfo.InvariantCulture) + " 项作业" : (done > 0 ? "其余都做完了，记得交付" : "清单里只有这一项");

            Grid more = new Grid();
            more.Margin = new Thickness(0, 1, 0, 4);
            more.ColumnDefinitions.Add(UI.Star());
            more.ColumnDefinitions.Add(UI.Auto());

            TextBlock hint = UI.Text(label, 10.5, B(Palette.Muted));
            hint.VerticalAlignment = VerticalAlignment.Center;
            more.Children.Add(hint);

            StackPanel expandContent = new StackPanel();
            expandContent.Orientation = Orientation.Horizontal;
            expandContent.VerticalAlignment = VerticalAlignment.Center;
            expandContent.Children.Add(UI.Text("展开全部", 10.5, B(Palette.InkSoft), FontWeights.Medium));
            Path chevron = UI.IconPath("M6 8l4 4 4-4", B(Palette.Muted), 11, 1.4);
            chevron.Margin = new Thickness(4, 0, 0, 0);
            expandContent.Children.Add(chevron);

            Button expand = UI.FlatButton(expandContent, 8, delegate { SetFocusMode(false); });
            expand.Height = 28;
            expand.Padding = new Thickness(9, 0, 9, 0);
            expand.Background = Brushes.Transparent;
            expand.ToolTip = "打开完整清单";
            UI.SetName(expand, "展开全部作业");
            UI.SetId(expand, "FocusExpandButton");
            UI.Hover(expand, Brushes.Transparent, B("#F2EBE0"));
            Grid.SetColumn(expand, 1);
            more.Children.Add(expand);

            _focusPanel.Children.Add(more);
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(delegate
            {
                if (_focusScroll != null) _focusScroll.ScrollToTop();
            }));
        }

        private TaskItem FocusTask()
        {
            TaskItem best = null;
            for (int i = 0; i < _state.Tasks.Count; i++)
            {
                TaskItem task = _state.Tasks[i];
                if (task.Done) continue;
                if (best == null || FocusComesBefore(task, best)) best = task;
            }
            if (best != null) return best;

            for (int i = 0; i < _state.Tasks.Count; i++)
            {
                TaskItem task = _state.Tasks[i];
                if (!task.Done) continue;
                if (best == null || FocusComesBefore(task, best)) best = task;
            }
            return best;
        }

        private static bool FocusComesBefore(TaskItem a, TaskItem b)
        {
            if (a.Priority != b.Priority) return a.Priority > b.Priority;
            DateTime ad = a.DueDate ?? DateTime.MaxValue;
            DateTime bd = b.DueDate ?? DateTime.MaxValue;
            if (ad != bd) return ad < bd;
            return a.CreatedDate > b.CreatedDate;
        }

        private Border BuildFocusHero(TaskItem task)
        {
            bool done = task.Done;
            Border card = new Border();
            card.CornerRadius = new CornerRadius(20);
            card.BorderThickness = new Thickness(1);
            card.BorderBrush = B(Palette.Rule);
            card.Background = done ? B("#FBF8F2") : B(Palette.Card);
            card.Padding = new Thickness(17, 16, 14, 15);
            card.Margin = new Thickness(0, 2, 0, 10);
            card.MinHeight = 170;
            card.SnapsToDevicePixels = true;
            card.Effect = UI.Shadow(18, 4, 0.06);

            Grid body = new Grid();
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            body.ColumnDefinitions.Add(UI.Star());

            Border stripe = new Border();
            stripe.Width = 4;
            stripe.CornerRadius = new CornerRadius(2);
            stripe.Background = PriorityBrush(task.Priority);
            stripe.VerticalAlignment = VerticalAlignment.Stretch;
            stripe.Margin = new Thickness(0, 2, 13, 2);
            stripe.Opacity = done ? 0.35 : 0.95;
            body.Children.Add(stripe);

            StackPanel content = new StackPanel();
            Grid.SetColumn(content, 1);
            body.Children.Add(content);

            Grid top = new Grid();
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            top.ColumnDefinitions.Add(UI.Star());
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            Grid checkGrid = new Grid();
            checkGrid.Width = 26;
            checkGrid.Height = 26;
            Border box = new Border();
            box.Width = 24;
            box.Height = 24;
            box.CornerRadius = new CornerRadius(7);
            box.BorderThickness = new Thickness(1.5);
            box.BorderBrush = done ? B(Palette.Green) : B(Palette.RuleStrong);
            box.Background = done ? B(Palette.Green) : B(Palette.Card);
            box.HorizontalAlignment = HorizontalAlignment.Center;
            box.VerticalAlignment = VerticalAlignment.Center;
            checkGrid.Children.Add(box);
            Path tick = UI.IconPath("M4.5 10.5l4 4 7-8", B(Colors.White), 13, 2.3);
            tick.HorizontalAlignment = HorizontalAlignment.Center;
            tick.VerticalAlignment = VerticalAlignment.Center;
            tick.Visibility = done ? Visibility.Visible : Visibility.Collapsed;
            checkGrid.Children.Add(tick);

            Button check = UI.FlatButton(checkGrid, 12, delegate { ToggleDone(task); });
            check.Width = 26;
            check.Height = 26;
            check.Padding = new Thickness(0);
            check.Background = Brushes.Transparent;
            check.ToolTip = done ? "取消完成" : "标记为做完，之后可以交付移除";
            UI.SetName(check, (done ? "取消完成：" : "标记完成：") + task.Title);
            UI.SetId(check, "FocusTaskCheck_" + task.Id);
            top.Children.Add(check);

            StackPanel meta = new StackPanel();
            meta.Orientation = Orientation.Horizontal;
            meta.VerticalAlignment = VerticalAlignment.Center;
            meta.Margin = new Thickness(9, 0, 0, 0);

            StackPanel priorityContent = new StackPanel();
            priorityContent.Orientation = Orientation.Horizontal;
            priorityContent.VerticalAlignment = VerticalAlignment.Center;
            Ellipse dot = new Ellipse();
            dot.Width = 5;
            dot.Height = 5;
            dot.Fill = PriorityBrush(task.Priority);
            dot.Margin = new Thickness(0, 0, 4, 0);
            priorityContent.Children.Add(dot);
            priorityContent.Children.Add(UI.Text(Priority.LongLabel(task.Priority), 10.5,
                PriorityBrush(task.Priority), FontWeights.SemiBold));

            Button priorityChip = UI.FlatButton(priorityContent, 6, delegate { CyclePriority(task); });
            priorityChip.Height = 23;
            priorityChip.Padding = new Thickness(7, 0, 7, 0);
            priorityChip.Background = PrioritySoftBrush(task.Priority);
            priorityChip.ToolTip = "点击切换轻重缓急";
            UI.SetName(priorityChip, "优先级：" + Priority.LongLabel(task.Priority) + "，点击切换");
            UI.SetId(priorityChip, "FocusPriority_" + task.Id);
            Brush priorityHover;
            if (task.Priority == Priority.High) priorityHover = B("#F5D8CD");
            else if (task.Priority == Priority.Medium) priorityHover = B("#F7E5C4");
            else priorityHover = B("#DDEAE4");
            UI.Hover(priorityChip, priorityChip.Background, priorityHover);
            meta.Children.Add(priorityChip);

            DateTime? dueDate = task.DueDate;
            if (dueDate.HasValue)
            {
                Brush dueColor = task.IsOverdue ? B(Palette.Accent)
                    : (task.IsDueToday ? B(Palette.Amber) : B(Palette.Muted));
                StackPanel due = new StackPanel();
                due.Orientation = Orientation.Horizontal;
                due.VerticalAlignment = VerticalAlignment.Center;
                due.Margin = new Thickness(8, 0, 0, 0);
                due.ToolTip = DateUtil.DueLabel(dueDate);
                Path cal = UI.IconPath("M4 6h16v10H4z M4 9h16 M7 4v3 M13 4v3",
                    dueColor, 12, 1.35);
                due.Children.Add(cal);
                TextBlock dueText = UI.Text(DateUtil.DueLabel(dueDate), 10.5, dueColor,
                    task.IsOverdue ? FontWeights.SemiBold : FontWeights.Medium);
                dueText.Margin = new Thickness(3, 0, 0, 0);
                due.Children.Add(dueText);
                meta.Children.Add(due);
            }

            if (done)
            {
                Border doneTag = new Border();
                doneTag.Background = B(Palette.GreenSoft);
                doneTag.CornerRadius = new CornerRadius(5);
                doneTag.Padding = new Thickness(5, 1, 5, 1);
                doneTag.Margin = new Thickness(8, 0, 0, 0);
                doneTag.VerticalAlignment = VerticalAlignment.Center;
                doneTag.Child = UI.Text("待交付", 9.8, B(Palette.Green), FontWeights.SemiBold);
                meta.Children.Add(doneTag);
            }

            Grid.SetColumn(meta, 1);
            top.Children.Add(meta);

            StackPanel actions = new StackPanel();
            actions.Orientation = Orientation.Horizontal;
            actions.VerticalAlignment = VerticalAlignment.Top;
            if (done)
            {
                Button deliver = UI.FlatButton("交付", 8, delegate { DeliverTask(task); });
                deliver.Height = 28;
                deliver.Padding = new Thickness(10, 0, 10, 0);
                deliver.Background = B(Palette.GreenSoft);
                deliver.Foreground = B(Palette.Green);
                deliver.FontSize = 11;
                deliver.FontWeight = FontWeights.SemiBold;
                deliver.ToolTip = "已经交给老师，从清单中移除";
                UI.SetName(deliver, "交付并移除：" + task.Title);
                UI.SetId(deliver, "FocusDeliver_" + task.Id);
                UI.Hover(deliver, B(Palette.GreenSoft), B("#D6E8DE"));
                actions.Children.Add(deliver);
            }
            else
            {
                Button edit = BuildCardIconButton(
                    "M4 16.5L4.8 13.2 13.2 4.8 15.8 7.4 7.4 15.8 4 16.5z M12.2 5.8l2.6 2.6",
                    "编辑作业", "FocusEdit_" + task.Id, delegate { OpenEdit(task); });
                actions.Children.Add(edit);
            }

            Button delete = BuildCardIconButton(
                "M5 7h10 M8 7V5h4v2 M6.5 7l.7 9h5.6l.7-9 M9 10v3 M11 10v3",
                "删除作业", "FocusDelete_" + task.Id, delegate { DeleteTask(task); });
            delete.Margin = new Thickness(1, 0, 0, 0);
            actions.Children.Add(delete);

            Grid.SetColumn(actions, 2);
            top.Children.Add(actions);
            content.Children.Add(top);

            TextBlock title = UI.Text(task.Title, 19.5,
                done ? B(Palette.Muted) : B(Palette.Ink), FontWeights.Bold);
            title.TextWrapping = TextWrapping.Wrap;
            title.LineHeight = 28;
            title.MaxHeight = 88;
            title.TextTrimming = TextTrimming.CharacterEllipsis;
            title.Margin = new Thickness(0, 11, 0, 0);
            title.Cursor = Cursors.Hand;
            title.ToolTip = "双击标题编辑作业";
            if (done) title.TextDecorations = TextDecorations.Strikethrough;
            title.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e)
            {
                if (e.ClickCount == 2)
                {
                    e.Handled = true;
                    OpenEdit(task);
                }
            };
            content.Children.Add(title);

            string foot;
            if (done) foot = "做完并交掉之后，它会离开清单。";
            else if (task.IsOverdue) foot = "已经逾期，先处理这一件。";
            else if (task.IsDueToday) foot = "今天截止，先做这一件。";
            else if (dueDate.HasValue) foot = "按截止时间，这一件最靠前。";
            else foot = "当前最要紧的一项。";
            TextBlock footText = UI.Text(foot, 10.5, B(Palette.Faint));
            footText.Margin = new Thickness(0, 8, 0, 0);
            content.Children.Add(footText);

            card.Child = body;
            return card;
        }

        private Border BuildFocusEmpty()
        {
            Border host = new Border();
            host.CornerRadius = new CornerRadius(20);
            host.BorderThickness = new Thickness(1);
            host.BorderBrush = B(Palette.Rule);
            host.Background = UI.Alpha(Color.FromRgb(0xFF, 0xFF, 0xFF), 0xD2);
            host.Padding = new Thickness(20, 26, 20, 24);
            host.Margin = new Thickness(0, 2, 0, 8);
            host.MinHeight = 250;

            StackPanel wrap = new StackPanel();
            wrap.HorizontalAlignment = HorizontalAlignment.Center;
            wrap.VerticalAlignment = VerticalAlignment.Center;

            Grid art = new Grid();
            art.Width = 62;
            art.Height = 62;
            art.HorizontalAlignment = HorizontalAlignment.Center;
            Border halo = new Border();
            halo.Width = 62;
            halo.Height = 62;
            halo.CornerRadius = new CornerRadius(31);
            halo.Background = B(Palette.AccentSoft);
            art.Children.Add(halo);
            Path glyph = UI.IconPath("M8 20l9 9 17-19", B(Palette.Accent), 25, 2.6);
            glyph.HorizontalAlignment = HorizontalAlignment.Center;
            glyph.VerticalAlignment = VerticalAlignment.Center;
            art.Children.Add(glyph);
            wrap.Children.Add(art);

            TextBlock title = UI.Text("今天还没有作业", 16, B(Palette.Ink), FontWeights.SemiBold);
            title.HorizontalAlignment = HorizontalAlignment.Center;
            title.Margin = new Thickness(0, 12, 0, 0);
            wrap.Children.Add(title);

            TextBlock note = UI.Text("写下一件最要紧的事，其他的可以慢慢加。", 11.2, B(Palette.Muted));
            note.TextWrapping = TextWrapping.Wrap;
            note.TextAlignment = TextAlignment.Center;
            note.MaxWidth = 250;
            note.HorizontalAlignment = HorizontalAlignment.Center;
            note.Margin = new Thickness(0, 5, 0, 0);
            wrap.Children.Add(note);

            StackPanel addContent = new StackPanel();
            addContent.Orientation = Orientation.Horizontal;
            addContent.VerticalAlignment = VerticalAlignment.Center;
            Path plus = UI.IconPath("M10 4v12 M4 10h12", B(Colors.White), 12, 2.0);
            plus.Margin = new Thickness(0, 0, 6, 0);
            addContent.Children.Add(plus);
            addContent.Children.Add(UI.Text("添加一项作业", 11.5, B(Colors.White), FontWeights.SemiBold));

            Button add = UI.FlatButton(addContent, 9, ExpandComposerAndFocus);
            add.Height = 34;
            add.Padding = new Thickness(13, 0, 13, 0);
            add.Background = B(Palette.Ink);
            add.HorizontalAlignment = HorizontalAlignment.Center;
            add.Margin = new Thickness(0, 13, 0, 0);
            UI.SetName(add, "添加一项作业");
            UI.SetId(add, "FocusEmptyAddButton");
            UI.Hover(add, B(Palette.Ink), B("#3A352C"));
            wrap.Children.Add(add);

            host.Child = wrap;
            return host;
        }

        private void UpdateScrollThumb()
        {
            if (_scroll == null || _scrollThumb == null || _scrollTrack == null) return;
            double extent = _scroll.ExtentHeight;
            double viewport = _scroll.ViewportHeight;
            double area = _scroll.ActualHeight;
            if (extent <= viewport + 1 || area <= 0)
            {
                _scrollThumb.Visibility = Visibility.Collapsed;
                return;
            }

            double thumbHeight = Math.Max(26, area * viewport / extent);
            double travel = area - thumbHeight;
            double maxOffset = extent - viewport;
            double offset = maxOffset <= 0 ? 0 : _scroll.VerticalOffset / maxOffset;
            _scrollThumb.Height = thumbHeight;
            _scrollThumb.Margin = new Thickness(0, offset * travel, 0, 0);
            _scrollThumb.Visibility = Visibility.Visible;
        }

        private void OnThumbMouseDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            _thumbDragging = true;
            _thumbStartY = e.GetPosition(_scrollTrack).Y;
            _thumbStartOffset = _scroll.VerticalOffset;
            _scrollThumb.CaptureMouse();
        }

        private void OnThumbMouseMove(object sender, MouseEventArgs e)
        {
            if (!_thumbDragging || _scroll == null) return;
            double area = _scroll.ActualHeight;
            double extent = _scroll.ExtentHeight;
            double viewport = _scroll.ViewportHeight;
            double travel = area - _scrollThumb.Height;
            if (travel <= 0 || extent <= viewport) return;
            double delta = e.GetPosition(_scrollTrack).Y - _thumbStartY;
            double target = _thumbStartOffset + delta / travel * (extent - viewport);
            if (target < 0) target = 0;
            if (target > _scroll.ScrollableHeight) target = _scroll.ScrollableHeight;
            _scroll.ScrollToVerticalOffset(target);
        }

        private void OnThumbMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!_thumbDragging) return;
            _thumbDragging = false;
            _scrollThumb.ReleaseMouseCapture();
            _scrollThumb.Background = UI.Alpha(Palette.Ink, 0x28);
        }

        private void OnScrollTrackClick(object sender, MouseButtonEventArgs e)
        {
            if (_scroll == null || _scrollThumb == null) return;
            if (e.OriginalSource == _scrollThumb) return;
            double y = e.GetPosition(_scrollTrack).Y;
            double target = y < _scrollThumb.Margin.Top
                ? _scroll.VerticalOffset - _scroll.ViewportHeight * 0.85
                : _scroll.VerticalOffset + _scroll.ViewportHeight * 0.85;
            if (target < 0) target = 0;
            if (target > _scroll.ScrollableHeight) target = _scroll.ScrollableHeight;
            _scroll.ScrollToVerticalOffset(target);
        }

        private void OnListMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Delta == 0) return;
            double target = _scroll.VerticalOffset - (e.Delta / 120.0) * 64.0;
            if (target < 0) target = 0;
            if (target > _scroll.ScrollableHeight) target = _scroll.ScrollableHeight;
            _scroll.ScrollToVerticalOffset(target);
            e.Handled = true;
        }

        private Border BuildFooter()
        {
            Border container = new Border();
            container.Margin = new Thickness(0, 10, 0, 0);

            StackPanel body = new StackPanel();
            body.Children.Add(UI.Divider());

            Grid stats = new Grid();
            stats.Margin = new Thickness(0, 9, 0, 0);
            stats.ColumnDefinitions.Add(UI.Star());
            stats.ColumnDefinitions.Add(UI.Auto());

            _progressText = UI.Text("", 11, B(Palette.Muted), FontWeights.Medium);
            stats.Children.Add(_progressText);

            _deliveredText = UI.Text("", 10.5, B(Palette.Faint));
            _deliveredText.HorizontalAlignment = HorizontalAlignment.Right;
            Grid.SetColumn(_deliveredText, 1);
            stats.Children.Add(_deliveredText);
            body.Children.Add(stats);

            _progressGrid = new Grid();
            _progressGrid.Height = 4;
            _progressGrid.Margin = new Thickness(0, 7, 0, 0);
            _progressGrid.ClipToBounds = true;
            _barFill = new ColumnDefinition();
            _barFill.Width = new GridLength(0, GridUnitType.Star);
            _barRest = new ColumnDefinition();
            _barRest.Width = new GridLength(1, GridUnitType.Star);
            _progressGrid.ColumnDefinitions.Add(_barFill);
            _progressGrid.ColumnDefinitions.Add(_barRest);

            Border track = UI.Rounded(B(Palette.Rule), 2);
            Grid.SetColumnSpan(track, 2);
            _progressGrid.Children.Add(track);

            Border fill = UI.Rounded(B(Palette.Green), 2);
            Grid.SetColumn(fill, 0);
            _progressGrid.Children.Add(fill);
            body.Children.Add(_progressGrid);

            container.Child = body;
            return container;
        }

        private void RefreshUI(bool keepScroll, string scrollToId)
        {
            if (_dateText == null) return;
            _state.RefreshDeliveryCounter();
            int pending = _state.PendingCount;
            int done = _state.DoneCount;

            _dateText.Text = DateUtil.HeaderDate(DateTime.Today);
            if (_state.Tasks.Count == 0)
            {
                _subtitleText.Text = "先写下今天第一项作业。";
            }
            else if (pending == 0)
            {
                _subtitleText.Text = "都做完了，记得交付。";
            }
            else
            {
                _subtitleText.Text = "还剩 " + pending.ToString(CultureInfo.InvariantCulture) + " 项，先做最要紧的一件。";
            }

            _addPlaceholder.Visibility = string.IsNullOrEmpty(_addBox.Text)
                ? Visibility.Visible : Visibility.Collapsed;
            UpdatePinVisual();
            UpdateComposerDueVisual();
            RebuildFilters();
            if (_focusMode) RebuildFocus();
            else RebuildList();
            UpdateFooter();
            UpdateWindowTitle();
            UpdateTrayStatus();

            double offset = _scroll == null ? 0 : _scroll.VerticalOffset;
            if (keepScroll && offset < 0) offset = 0;
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(delegate
            {
                if (_scroll == null) return;
                _scroll.ScrollToVerticalOffset(keepScroll ? offset : 0);
                if (!string.IsNullOrEmpty(scrollToId) && _cards.ContainsKey(scrollToId))
                {
                    _cards[scrollToId].BringIntoView();
                }
            }));
        }

        private void RebuildList()
        {
            if (_listPanel == null) return;
            _cards.Clear();
            _listPanel.Children.Clear();

            List<TaskItem> tasks = VisibleTasks();
            if (tasks.Count == 0)
            {
                _listPanel.Children.Add(BuildEmptyState());
                Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(UpdateScrollThumb));
                return;
            }

            for (int i = 0; i < tasks.Count; i++)
            {
                TaskItem task = tasks[i];
                Border card = BuildTaskCard(task);
                _cards[task.Id] = card;
                _listPanel.Children.Add(card);
            }
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(UpdateScrollThumb));
        }

        private List<TaskItem> VisibleTasks()
        {
            IEnumerable<TaskItem> query = _state.Tasks;
            if (_filter == "high")
            {
                query = query.Where(delegate(TaskItem t) { return t.Priority == Priority.High; });
            }
            else if (_filter == "done")
            {
                query = query.Where(delegate(TaskItem t) { return t.Done; });
            }

            return query
                .OrderBy(delegate(TaskItem t) { return t.Done ? 1 : 0; })
                .ThenByDescending(delegate(TaskItem t) { return t.Priority; })
                .ThenBy(delegate(TaskItem t) { return t.DueDate ?? DateTime.MaxValue; })
                .ThenByDescending(delegate(TaskItem t) { return t.CreatedDate; })
                .ToList();
        }

        private Border BuildTaskCard(TaskItem task)
        {
            bool done = task.Done;
            Border card = new Border();
            card.Tag = task.Id;
            card.MinHeight = 66;
            card.Margin = new Thickness(0, 0, 0, 8);
            card.CornerRadius = new CornerRadius(15);
            card.BorderThickness = new Thickness(1);
            card.BorderBrush = B(Palette.Rule);
            card.Background = done ? B("#FBF8F2") : B(Palette.Card);
            card.SnapsToDevicePixels = true;
            Brush normalBackground = done ? B("#FBF8F2") : B(Palette.Card);
            Brush hoverBackground = done ? B("#FFFDF8") : B(Palette.CardHover);
            card.MouseEnter += delegate
            {
                card.Background = hoverBackground;
                card.BorderBrush = B(Palette.RuleStrong);
            };
            card.MouseLeave += delegate
            {
                card.Background = normalBackground;
                card.BorderBrush = B(Palette.Rule);
            };

            Grid body = new Grid();
            body.Margin = new Thickness(13, 11, 10, 11);
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            body.ColumnDefinitions.Add(UI.Star());
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            Border stripe = new Border();
            stripe.Width = 3;
            stripe.CornerRadius = new CornerRadius(1.5);
            stripe.Background = PriorityBrush(task.Priority);
            stripe.VerticalAlignment = VerticalAlignment.Stretch;
            stripe.Margin = new Thickness(0, 2, 8, 2);
            stripe.Opacity = done ? 0.35 : 0.9;
            body.Children.Add(stripe);

            int contentColumn = 2;
            Grid.SetColumn(stripe, 0);

            Border checkHost = new Border();
            checkHost.Width = 22;
            checkHost.Height = 22;
            checkHost.Background = Brushes.Transparent;
            checkHost.VerticalAlignment = VerticalAlignment.Top;
            checkHost.Margin = new Thickness(0, 1, 0, 0);

            Grid checkGrid = new Grid();
            Border box = new Border();
            box.Width = 20;
            box.Height = 20;
            box.CornerRadius = new CornerRadius(6);
            box.BorderThickness = new Thickness(1.5);
            box.BorderBrush = done ? B(Palette.Green) : B(Palette.RuleStrong);
            box.Background = done ? B(Palette.Green) : B(Palette.Card);
            box.HorizontalAlignment = HorizontalAlignment.Center;
            box.VerticalAlignment = VerticalAlignment.Center;
            checkGrid.Children.Add(box);

            Path tick = UI.IconPath("M4.5 10.5l4 4 7-8", B(Colors.White), 12, 2.3);
            tick.HorizontalAlignment = HorizontalAlignment.Center;
            tick.VerticalAlignment = VerticalAlignment.Center;
            tick.Visibility = done ? Visibility.Visible : Visibility.Collapsed;
            checkGrid.Children.Add(tick);

            Button check = UI.FlatButton(checkGrid, 11, delegate { ToggleDone(task); });
            check.Width = 22;
            check.Height = 22;
            check.Padding = new Thickness(0);
            check.Background = Brushes.Transparent;
            check.ToolTip = done ? "取消完成" : "标记为做完，之后可以交付移除";
            UI.SetName(check, (done ? "取消完成：" : "标记完成：") + task.Title);
            UI.SetId(check, "TaskCheck_" + task.Id);
            check.MouseEnter += delegate
            {
                if (!task.Done) box.BorderBrush = B(Palette.Accent);
            };
            check.MouseLeave += delegate
            {
                if (!task.Done) box.BorderBrush = B(Palette.RuleStrong);
            };
            Grid.SetColumn(check, 1);
            body.Children.Add(check);

            StackPanel textArea = new StackPanel();
            textArea.Margin = new Thickness(8, 0, 0, 0);
            textArea.VerticalAlignment = VerticalAlignment.Center;
            textArea.Cursor = Cursors.Hand;
            textArea.ToolTip = "双击标题编辑作业";

            TextBlock title = UI.Text(task.Title, 14.2,
                done ? B(Palette.Muted) : B(Palette.Ink), FontWeights.SemiBold);
            title.TextWrapping = TextWrapping.Wrap;
            title.LineHeight = 20;
            title.MaxHeight = 40;
            title.TextTrimming = TextTrimming.CharacterEllipsis;
            if (done) title.TextDecorations = TextDecorations.Strikethrough;
            title.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e)
            {
                if (e.ClickCount == 2)
                {
                    e.Handled = true;
                    OpenEdit(task);
                }
            };
            textArea.Children.Add(title);

            StackPanel meta = new StackPanel();
            meta.Orientation = Orientation.Horizontal;
            meta.Margin = new Thickness(0, 5, 0, 0);
            meta.VerticalAlignment = VerticalAlignment.Center;

            StackPanel priorityContent = new StackPanel();
            priorityContent.Orientation = Orientation.Horizontal;
            priorityContent.VerticalAlignment = VerticalAlignment.Center;
            Ellipse dot = new Ellipse();
            dot.Width = 5;
            dot.Height = 5;
            dot.Fill = PriorityBrush(task.Priority);
            dot.Margin = new Thickness(0, 0, 4, 0);
            priorityContent.Children.Add(dot);
            TextBlock priorityText = UI.Text(Priority.LongLabel(task.Priority), 10.2,
                PriorityBrush(task.Priority), FontWeights.SemiBold);
            priorityContent.Children.Add(priorityText);

            Button priorityChip = UI.FlatButton(priorityContent, 6, delegate { CyclePriority(task); });
            priorityChip.Height = 20;
            priorityChip.Padding = new Thickness(6, 0, 6, 0);
            priorityChip.Background = PrioritySoftBrush(task.Priority);
            priorityChip.ToolTip = "点击切换轻重缓急";
            UI.SetName(priorityChip, "优先级：" + Priority.LongLabel(task.Priority) + "，点击切换");
            UI.SetId(priorityChip, "TaskPriority_" + task.Id);
            Brush priorityHover;
            if (task.Priority == Priority.High) priorityHover = B("#F5D8CD");
            else if (task.Priority == Priority.Medium) priorityHover = B("#F7E5C4");
            else priorityHover = B("#DDEAE4");
            UI.Hover(priorityChip, priorityChip.Background, priorityHover);
            meta.Children.Add(priorityChip);

            DateTime? dueDate = task.DueDate;
            if (dueDate.HasValue)
            {
                Brush dueColor = task.IsOverdue ? B(Palette.Accent)
                    : (task.IsDueToday ? B(Palette.Amber) : B(Palette.Muted));
                StackPanel due = new StackPanel();
                due.Orientation = Orientation.Horizontal;
                due.VerticalAlignment = VerticalAlignment.Center;
                due.Margin = new Thickness(7, 0, 0, 0);
                due.ToolTip = DateUtil.DueLabel(dueDate);
                Path cal = UI.IconPath("M4 6h12v10H4z M4 9h12 M7 4v3 M13 4v3",
                    dueColor, 11, 1.35);
                due.Children.Add(cal);
                TextBlock dueText = UI.Text(DateUtil.DueLabel(dueDate), 10.2, dueColor,
                    task.IsOverdue ? FontWeights.SemiBold : FontWeights.Medium);
                dueText.Margin = new Thickness(3, 0, 0, 0);
                due.Children.Add(dueText);
                meta.Children.Add(due);
            }

            if (done)
            {
                Border doneTag = new Border();
                doneTag.Background = B(Palette.GreenSoft);
                doneTag.CornerRadius = new CornerRadius(5);
                doneTag.Padding = new Thickness(5, 1, 5, 1);
                doneTag.Margin = new Thickness(7, 0, 0, 0);
                doneTag.VerticalAlignment = VerticalAlignment.Center;
                TextBlock doneText = UI.Text("待交付", 9.8, B(Palette.Green), FontWeights.SemiBold);
                doneTag.Child = doneText;
                meta.Children.Add(doneTag);
            }

            textArea.Children.Add(meta);
            Grid.SetColumn(textArea, contentColumn);
            body.Children.Add(textArea);

            StackPanel actionArea = new StackPanel();
            actionArea.Orientation = Orientation.Horizontal;
            actionArea.VerticalAlignment = VerticalAlignment.Center;
            actionArea.Margin = new Thickness(8, 0, 0, 0);
            actionArea.Opacity = 0.82;
            card.MouseEnter += delegate { actionArea.Opacity = 1; };
            card.MouseLeave += delegate { actionArea.Opacity = 0.82; };

            if (done)
            {
                Button deliver = UI.FlatButton("交付", 8, delegate { DeliverTask(task); });
                deliver.Height = 25;
                deliver.Padding = new Thickness(8, 0, 8, 0);
                deliver.Background = B(Palette.GreenSoft);
                deliver.Foreground = B(Palette.Green);
                deliver.FontSize = 10.5;
                deliver.FontWeight = FontWeights.SemiBold;
                deliver.ToolTip = "已经交给老师，从清单中移除";
                UI.SetName(deliver, "交付并移除：" + task.Title);
                UI.SetId(deliver, "TaskDeliver_" + task.Id);
                UI.Hover(deliver, B(Palette.GreenSoft), B("#D6E8DE"));
                actionArea.Children.Add(deliver);
            }
            else
            {
                Button edit = BuildCardIconButton(
                    "M4 16.5L4.8 13.2 13.2 4.8 15.8 7.4 7.4 15.8 4 16.5z M12.2 5.8l2.6 2.6",
                    "编辑作业", "TaskEdit_" + task.Id, delegate { OpenEdit(task); });
                actionArea.Children.Add(edit);
            }

            Button delete = BuildCardIconButton(
                "M5 7h10 M8 7V5h4v2 M6.5 7l.7 9h5.6l.7-9 M9 10v3 M11 10v3",
                "删除作业", "TaskDelete_" + task.Id, delegate { DeleteTask(task); });
            delete.Margin = new Thickness(1, 0, 0, 0);
            actionArea.Children.Add(delete);

            Grid.SetColumn(actionArea, 3);
            body.Children.Add(actionArea);

            card.Child = body;
            return card;
        }

        private Button BuildCardIconButton(string path, string name, string id, Action onClick)
        {
            Path icon = UI.IconPath(path, B(Palette.Muted), 13.5, 1.4);
            Button button = UI.FlatButton(icon, 7, onClick);
            button.Width = 26;
            button.Height = 26;
            button.Background = Brushes.Transparent;
            button.ToolTip = name;
            UI.SetName(button, name);
            UI.SetId(button, id);
            UI.Hover(button, Brushes.Transparent, B("#F3EDE2"));
            return button;
        }

        private Border BuildEmptyState()
        {
            bool hasAny = _state.Tasks.Count > 0;
            string heading;
            string note;
            if (_filter == "high")
            {
                heading = "没有高优先作业";
                note = "重要的事也可以慢慢加进来，或者把某一项的优先级调高。";
            }
            else if (_filter == "done")
            {
                heading = "没有待交付的作业";
                note = "勾选一项作业的圆圈，它就会来到这里等你交付。";
            }
            else if (!hasAny)
            {
                heading = "还没有作业";
                note = "把今天要做的事写下来，用高 / 中 / 低分清轻重缓急。做完后点“交付”，它就会离开清单。";
            }
            else
            {
                heading = "这里暂时空着";
                note = "换个筛选条件看看。";
            }

            StackPanel wrap = new StackPanel();
            wrap.HorizontalAlignment = HorizontalAlignment.Center;
            wrap.VerticalAlignment = VerticalAlignment.Center;
            wrap.Margin = new Thickness(0, 12, 0, 22);

            Grid art = new Grid();
            art.Width = 74;
            art.Height = 74;
            art.HorizontalAlignment = HorizontalAlignment.Center;

            Border halo = new Border();
            halo.Width = 74;
            halo.Height = 74;
            halo.CornerRadius = new CornerRadius(37);
            halo.Background = B(Palette.AccentSoft);
            art.Children.Add(halo);

            Path glyph = UI.IconPath("M8 20l9 9 17-19", B(Palette.Accent), 30, 2.6);
            glyph.HorizontalAlignment = HorizontalAlignment.Center;
            glyph.VerticalAlignment = VerticalAlignment.Center;
            art.Children.Add(glyph);
            wrap.Children.Add(art);

            TextBlock title = UI.Text(heading, 15.5, B(Palette.Ink), FontWeights.SemiBold);
            title.HorizontalAlignment = HorizontalAlignment.Center;
            title.Margin = new Thickness(0, 14, 0, 0);
            wrap.Children.Add(title);

            TextBlock body = UI.Text(note, 11.5, B(Palette.Muted));
            body.TextWrapping = TextWrapping.Wrap;
            body.TextAlignment = TextAlignment.Center;
            body.LineHeight = 18;
            body.MaxWidth = 252;
            body.HorizontalAlignment = HorizontalAlignment.Center;
            body.Margin = new Thickness(0, 6, 0, 0);
            wrap.Children.Add(body);

            if (!hasAny)
            {
                StackPanel content = new StackPanel();
                content.Orientation = Orientation.Horizontal;
                content.VerticalAlignment = VerticalAlignment.Center;
                Path plus = UI.IconPath("M10 4v12 M4 10h12", B(Colors.White), 12, 2.0);
                plus.Margin = new Thickness(0, 0, 6, 0);
                content.Children.Add(plus);
                content.Children.Add(UI.Text("添加第一项作业", 11.5, B(Colors.White), FontWeights.SemiBold));

                Button addNow = UI.FlatButton(content, 10, FocusComposer);
                addNow.Height = 34;
                addNow.Padding = new Thickness(14, 0, 14, 0);
                addNow.Background = B(Palette.Ink);
                addNow.HorizontalAlignment = HorizontalAlignment.Center;
                addNow.Margin = new Thickness(0, 14, 0, 0);
                UI.SetName(addNow, "添加第一项作业");
                UI.Hover(addNow, B(Palette.Ink), B("#3A352C"));
                wrap.Children.Add(addNow);
            }

            Border host = new Border();
            host.Child = wrap;
            host.VerticalAlignment = VerticalAlignment.Stretch;
            host.MinHeight = 260;
            host.Background = Brushes.Transparent;
            if (_scroll != null)
            {
                Binding heightBinding = new Binding("ActualHeight");
                heightBinding.Source = _scroll;
                host.SetBinding(FrameworkElement.HeightProperty, heightBinding);
            }
            return host;
        }

        private void FocusComposer()
        {
            if (_addBox == null) return;
            if (!_state.ComposerExpanded) SetComposerExpanded(true);
            _addBox.Focus();
            _addCard.BorderBrush = B(Palette.Accent);
        }

        private void UpdateFooter()
        {
            if (_progressText == null) return;
            int total = _state.Tasks.Count;
            int done = _state.DoneCount;
            double ratio = total == 0 ? 0 : (double)done / total;
            _progressText.Text = total == 0
                ? "清单是空的"
                : "已做完 " + done.ToString(CultureInfo.InvariantCulture) + " / " + total.ToString(CultureInfo.InvariantCulture);
            _deliveredText.Text = "今日已交付 " + _state.DeliveredCount.ToString(CultureInfo.InvariantCulture) + " 项";
            _barFill.Width = new GridLength(ratio, GridUnitType.Star);
            _barRest.Width = new GridLength(1 - ratio, GridUnitType.Star);
        }

        private void UpdateWindowTitle()
        {
            int pending = _state.PendingCount;
            int done = _state.DoneCount;
            if (pending > 0)
            {
                Title = "作业清单 · " + pending.ToString(CultureInfo.InvariantCulture) + " 项待做";
            }
            else if (done > 0)
            {
                Title = "作业清单 · " + done.ToString(CultureInfo.InvariantCulture) + " 项待交付";
            }
            else
            {
                Title = "作业清单";
            }
        }

        private void UpdateTrayStatus()
        {
            if (_tray == null) return;
            _tray.UpdateStatus(_state.PendingCount, _state.DoneCount);
        }

        private void HighlightCard(string id)
        {
            if (string.IsNullOrEmpty(id) || !_cards.ContainsKey(id)) return;
            Border card = _cards[id];
            card.BorderBrush = B(Palette.Accent);
            card.Background = B(Palette.AccentSoft);
            DispatcherTimer timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromMilliseconds(950);
            timer.Tick += delegate
            {
                timer.Stop();
                TaskItem task = FindTask(id);
                if (task != null)
                {
                    card.BorderBrush = B(Palette.Rule);
                    card.Background = task.Done ? B("#FBF8F2") : B(Palette.Card);
                }
            };
            timer.Start();
        }

        private TaskItem FindTask(string id)
        {
            for (int i = 0; i < _state.Tasks.Count; i++)
            {
                if (string.Equals(_state.Tasks[i].Id, id, StringComparison.Ordinal)) return _state.Tasks[i];
            }
            return null;
        }

        private void CyclePriority(TaskItem task)
        {
            task.Priority = Priority.Next(task.Priority);
            Persist();
            RefreshUI(true, task.Id);
        }

        private void ToggleDone(TaskItem task)
        {
            task.Done = !task.Done;
            if (task.Done)
            {
                task.CompletedAt = DateTime.Now.ToString("o", CultureInfo.InvariantCulture);
            }
            else
            {
                task.CompletedAt = null;
            }
            Persist();
            RefreshUI(true, task.Id);
        }

        private void DeliverTask(TaskItem task)
        {
            int index = _state.Tasks.IndexOf(task);
            if (index < 0) return;
            _state.Tasks.RemoveAt(index);
            _state.RecordDelivery();
            Persist();
            RefreshUI(true, null);

            ShowToast("已交付「" + ShortTitle(task.Title) + "」，并移出清单。",
                "撤销", delegate
                {
                    int insertAt = Math.Min(index, _state.Tasks.Count);
                    _state.Tasks.Insert(insertAt, task);
                    _state.UndoDeliveryRecord();
                    Persist();
                    RefreshUI(true, task.Id);
                }, 7000);
        }

        private void DeleteTask(TaskItem task)
        {
            int index = _state.Tasks.IndexOf(task);
            if (index < 0) return;
            _state.Tasks.RemoveAt(index);
            Persist();
            RefreshUI(true, null);
            ShowToast("已删除「" + ShortTitle(task.Title) + "」。", "撤销", delegate
            {
                int insertAt = Math.Min(index, _state.Tasks.Count);
                _state.Tasks.Insert(insertAt, task);
                Persist();
                RefreshUI(true, task.Id);
            }, 7000);
        }

        private static string ShortTitle(string title)
        {
            if (string.IsNullOrEmpty(title)) return "未命名作业";
            string clean = title.Trim();
            if (clean.Length <= 16) return clean;
            return clean.Substring(0, 16) + "…";
        }

        private void OnWindowPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.N)
            {
                e.Handled = true;
                FocusComposer();
                return;
            }
            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.OemComma)
            {
                e.Handled = true;
                ShowSettings();
                return;
            }
            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.W)
            {
                e.Handled = true;
                HideToTray();
                return;
            }
            if (e.Key == Key.Escape)
            {
                if (_overlayHost != null && _overlayHost.Visibility == Visibility.Visible)
                {
                    e.Handled = true;
                    CloseOverlay();
                    return;
                }
                if (_setupPopupOpen(_settingsPopup) || _setupPopupOpen(_duePopup))
                {
                    e.Handled = true;
                    ClosePopups();
                }
            }
        }

        private static bool _setupPopupOpen(Popup popup)
        {
            return popup != null && popup.IsOpen;
        }

        private void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            Opacity = 1;
            BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, Ms(220)));
            TranslateTransform slide = new TranslateTransform(0, 10);
            _panel.RenderTransform = slide;
            DoubleAnimation move = new DoubleAnimation(10, 0, Ms(260));
            move.EasingFunction = EaseOut();
            slide.BeginAnimation(TranslateTransform.YProperty, move);

            if (!_startupLaunch && _state.ComposerExpanded)
            {
                _addBox.Focus();
            }

            Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(ShowStartupNotice));
        }

        private void ShowStartupNotice()
        {
            if (_store == null) return;
            if (!string.IsNullOrEmpty(_store.LoadWarning))
            {
                ShowToast(_store.LoadWarning, null, null, 9000);
                return;
            }
            if (!string.IsNullOrEmpty(_autostartError))
            {
                ShowToast(_autostartError, null, null, 7000);
                return;
            }
            if (_store.WasFirstRun)
            {
                string message = "清单只保存在这台电脑";
                try
                {
                    if (StartupManager.IsCurrentExecutableRegistered()) message += "，已加入开机启动";
                }
                catch
                {
                }
                message += "。拖动顶部移动，拖动底部调高度。";
                ShowToast(message, null, null, 6500);
            }
        }

        private void OnWindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!_allowClose)
            {
                e.Cancel = true;
                HideToTray();
                return;
            }
            SaveEverything();
        }

        private void OnWindowClosed(object sender, EventArgs e)
        {
            if (_dayTimer != null) _dayTimer.Stop();
            if (_tray != null)
            {
                _tray.Dispose();
                _tray = null;
            }
        }

        private void OnDayTimerTick(object sender, EventArgs e)
        {
            string today = DateUtil.TodayKey();
            if (string.Equals(today, _todayKey, StringComparison.Ordinal)) return;
            _todayKey = today;
            _state.RefreshDeliveryCounter();
            Persist();
            RefreshUI(true, null);
        }

        private void HeaderMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState != MouseButtonState.Pressed) return;
            if (e.ClickCount > 1) return;
            try
            {
                DragMove();
            }
            catch
            {
            }
            if (!IsPositionUsable(Left, Top)) PositionRightSide(false);
            SaveWindowPosition();
            Persist();
        }

        private void ToggleTopmost()
        {
            _state.Settings.Topmost = !_state.Settings.Topmost;
            Topmost = _state.Settings.Topmost;
            UpdatePinVisual();
            Persist();
            if (_settingsPopup != null && _settingsPopup.IsOpen)
            {
                _settingsPopup.Child = BuildSettingsPanel();
            }
        }

        private void UpdatePinVisual()
        {
            if (_pinButton == null || _pinIcon == null) return;
            bool pinned = _state != null && _state.Settings.Topmost;
            _pinButton.Background = pinned ? B(Palette.AccentSoft) : Brushes.Transparent;
            _pinIcon.Stroke = pinned ? B(Palette.Accent) : B(Palette.Muted);
            _pinButton.ToolTip = pinned ? "已置顶，点击取消" : "点击让窗口始终置顶";
            UI.SetName(_pinButton, pinned ? "取消窗口置顶" : "窗口置顶");
        }

        private void PositionRightSide(bool persist)
        {
            Rect work = SystemParameters.WorkArea;
            Left = work.Right - Width - 18;
            Top = work.Top + Math.Max(0, (work.Height - Height) / 2);
            _state.HasWindowPosition = true;
            _state.WindowLeft = Left;
            _state.WindowTop = Top;
            if (persist) Persist();
        }

        private void SetupInitialPosition()
        {
            if (_state.HasWindowPosition && IsPositionUsable(_state.WindowLeft, _state.WindowTop))
            {
                Left = _state.WindowLeft;
                Top = _state.WindowTop;
                return;
            }
            PositionRightSide(false);
        }

        private bool IsPositionUsable(double left, double top)
        {
            if (double.IsNaN(left) || double.IsNaN(top)
                || double.IsInfinity(left) || double.IsInfinity(top)) return false;
            Rect window = new Rect(left, top, Width, Height);
            Rect virtualScreen = new Rect(SystemParameters.VirtualScreenLeft,
                SystemParameters.VirtualScreenTop, SystemParameters.VirtualScreenWidth,
                SystemParameters.VirtualScreenHeight);
            Rect visible = Rect.Intersect(window, virtualScreen);
            return !visible.IsEmpty && visible.Width >= 120 && visible.Height >= 120;
        }

        private void SaveWindowPosition()
        {
            if (double.IsNaN(Left) || double.IsNaN(Top)) return;
            _state.HasWindowPosition = true;
            _state.WindowLeft = Left;
            _state.WindowTop = Top;
            if (!double.IsNaN(Height) && Height > 0) _state.WindowHeight = Height;
        }

        public void SaveEverything()
        {
            if (_store == null || _state == null) return;
            SaveWindowPosition();
            try
            {
                _store.Save(_state);
            }
            catch (Exception ex)
            {
                try { _store.Log("SaveEverything failed: " + ex); }
                catch { }
            }
        }

        public void PrepareForShutdown()
        {
            _exiting = true;
            _allowClose = true;
            ClosePopups();
            SaveEverything();
            if (_tray != null)
            {
                _tray.Dispose();
                _tray = null;
            }
        }

        private void Persist()
        {
            if (_store == null || _state == null) return;
            try
            {
                _store.Save(_state);
            }
            catch (Exception ex)
            {
                _store.Log("Save failed: " + ex);
                if (!_exiting) ShowToast("保存失败：" + ex.Message, null, null, 6000);
            }
        }

        private void InitializeAutostart()
        {
            if (_noAutostart) return;
            try
            {
                bool active = StartupManager.IsCurrentExecutableRegistered();
                if (_state.Settings.Autostart && !active)
                {
                    StartupManager.EnsureEnabled(false);
                    active = StartupManager.IsCurrentExecutableRegistered();
                }
                if (active != _state.Settings.Autostart)
                {
                    _state.Settings.Autostart = active;
                    try { _store.Save(_state); }
                    catch { }
                }
                if (_state.Settings.Autostart && !active)
                {
                    _autostartError = "开机启动设置没有生效，请稍后手动打开。";
                }
            }
            catch (Exception ex)
            {
                _store.Log("Autostart setup failed: " + ex);
                _state.Settings.Autostart = false;
                _autostartError = "开机启动设置失败：" + ex.Message;
            }
        }

        private bool AutostartActive
        {
            get
            {
                try { return StartupManager.IsCurrentExecutableRegistered(); }
                catch { return false; }
            }
        }

        private void SetAutostartFromTray(bool enabled)
        {
            try
            {
                StartupManager.SetEnabled(enabled);
                bool actual = StartupManager.IsCurrentExecutableRegistered();
                _state.Settings.Autostart = actual;
                Persist();
                if (actual != enabled)
                {
                    ShowToast("开机自启设置没有生效，可能被系统策略拦截。", null, null, 6000);
                }
            }
            catch (Exception ex)
            {
                _store.Log("Set autostart failed: " + ex);
                _state.Settings.Autostart = false;
                Persist();
                ShowToast("开机自启设置失败：" + ex.Message, null, null, 6500);
            }
            if (_settingsPopup != null && _settingsPopup.IsOpen)
            {
                _settingsPopup.Child = BuildSettingsPanel();
            }
        }

        private void SetupTray()
        {
            _tray = new TrayIcon(ShowFromTray, HideToTray, ExitApp,
                delegate { return AutostartActive; },
                SetAutostartFromTray, _store.DataDir);
            UpdateTrayStatus();
        }

        public void ShowFromTray()
        {
            if (!IsVisible) Show();
            if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
            if (!IsPositionUsable(Left, Top)) PositionRightSide(true);
            ShowInTaskbar = true;
            Topmost = _state.Settings.Topmost;
            Activate();
            Focus();
        }

        private void HideToTray()
        {
            if (!IsVisible) return;
            ClosePopups();
            if (_overlayHost != null && _overlayHost.Visibility == Visibility.Visible) CloseOverlay();
            SaveWindowPosition();
            Persist();
            Hide();
            if (!_balloonShown && _tray != null)
            {
                _balloonShown = true;
                _tray.ShowBalloon("作业清单已收进托盘", "点右下角的图标，或者再次打开程序，就能回来。");
            }
        }

        private void ExitApp()
        {
            _exiting = true;
            _allowClose = true;
            ClosePopups();
            SaveEverything();
            if (_tray != null)
            {
                _tray.Dispose();
                _tray = null;
            }
            Application.Current.Shutdown();
        }

        private void ResetToRightSide()
        {
            CloseSettingsPopup();
            WindowState = WindowState.Normal;
            PositionRightSide(true);
            Topmost = _state.Settings.Topmost;
            Activate();
            ShowToast("窗口已回到屏幕右侧。", null, null, 3500);
        }

        private void ResetWindowHeight()
        {
            Height = ClampWindowHeight(_focusMode ? CompactHeight : DetailHeight);
            _state.WindowHeight = Height;
            if (!IsPositionUsable(Left, Top)) PositionRightSide(true);
            SaveWindowHeight();
            ShowToast("已恢复默认高度。", null, null, 3200);
        }

        private void OpenDataFolder()
        {
            try
            {
                string dir = _store.DataDir;
                Directory.CreateDirectory(dir);
                Process.Start("explorer.exe", "\"" + dir + "\"");
            }
            catch (Exception ex)
            {
                ShowToast("打开数据文件夹失败：" + ex.Message, null, null, 6000);
            }
        }

        private void ShowSettings()
        {
            CloseDuePopup();
            if (_settingsOpenAtMouseDown)
            {
                _settingsOpenAtMouseDown = false;
                CloseSettingsPopup();
                return;
            }
            if (_settingsPopup != null && _settingsPopup.IsOpen)
            {
                CloseSettingsPopup();
                return;
            }

            Popup popup = new Popup();
            popup.PlacementTarget = _settingsButton;
            popup.Placement = PlacementMode.Bottom;
            popup.HorizontalOffset = -252;
            popup.VerticalOffset = 8;
            popup.StaysOpen = false;
            popup.AllowsTransparency = true;
            popup.PopupAnimation = PopupAnimation.Fade;
            popup.Child = BuildSettingsPanel();
            popup.IsOpen = true;
            _settingsPopup = popup;
        }

        private void CloseSettingsPopup()
        {
            if (_settingsPopup != null) _settingsPopup.IsOpen = false;
        }

        private Border BuildSettingsPanel()
        {
            Border wrap = new Border();
            wrap.Margin = new Thickness(10);
            wrap.Width = 300;
            wrap.Background = B(Palette.Card);
            wrap.CornerRadius = new CornerRadius(16);
            wrap.BorderBrush = B(Palette.Rule);
            wrap.BorderThickness = new Thickness(1);
            wrap.Padding = new Thickness(16, 14, 16, 12);
            wrap.Effect = UI.Shadow(26, 8, 0.18);

            StackPanel body = new StackPanel();

            Grid heading = new Grid();
            heading.ColumnDefinitions.Add(UI.Star());
            heading.ColumnDefinitions.Add(UI.Auto());
            TextBlock title = UI.Text("设置", 15, B(Palette.Ink), FontWeights.SemiBold);
            heading.Children.Add(title);
            TextBlock version = UI.Text("v" + VersionText, 9.5, B(Palette.Faint), FontWeights.Medium);
            version.FontFamily = UI.FontNumeric;
            version.HorizontalAlignment = HorizontalAlignment.Right;
            Grid.SetColumn(version, 1);
            heading.Children.Add(version);
            body.Children.Add(heading);

            TextBlock note = UI.Text("按你的习惯调整它。", 10.2, B(Palette.Muted));
            note.Margin = new Thickness(0, 3, 0, 0);
            body.Children.Add(note);

            Border divider = UI.Divider();
            divider.Margin = new Thickness(0, 11, 0, 3);
            body.Children.Add(divider);

            bool auto = AutostartActive;
            body.Children.Add(BuildToggleRow("紧凑焦点", "只显示当前最要紧的一项",
                _state.FocusMode, delegate { SetFocusMode(!_state.FocusMode); }));
            body.Children.Add(BuildToggleRow("窗口置顶", "始终浮在其他窗口上方",
                _state.Settings.Topmost, ToggleTopmost));
            body.Children.Add(BuildToggleRow("开机自启", "登录 Windows 后自动出现",
                auto, delegate { SetAutostartFromTray(!AutostartActive); }));

            Border divider2 = UI.Divider();
            divider2.Margin = new Thickness(0, 8, 0, 6);
            body.Children.Add(divider2);

            body.Children.Add(BuildSettingsAction("回到屏幕右侧", "M4 10h11 M11 6l4 4-4 4",
                delegate { CloseSettingsPopup(); ResetToRightSide(); }, false));
            body.Children.Add(BuildSettingsAction("恢复默认高度", "M4 7h12 M4 13h12 M7 4l-3 3 3 3 M13 10l3 3-3 3",
                delegate { CloseSettingsPopup(); ResetWindowHeight(); }, false));
            body.Children.Add(BuildSettingsAction("打开数据文件夹", "M3.5 6.5h5l1.5 2h6.5v7h-13z",
                delegate { CloseSettingsPopup(); OpenDataFolder(); }, false));

            TextBlock footer = UI.Text("数据只保存在这台电脑 · 修改会自动保存", 9.2, B(Palette.Faint));
            footer.Margin = new Thickness(0, 9, 0, 0);
            footer.HorizontalAlignment = HorizontalAlignment.Center;
            body.Children.Add(footer);

            wrap.Child = body;
            return wrap;
        }

        private Grid BuildToggleRow(string title, string description, bool value, Action toggle)
        {
            Grid row = new Grid();
            row.Margin = new Thickness(0, 8, 0, 8);
            row.ColumnDefinitions.Add(UI.Star());
            row.ColumnDefinitions.Add(UI.Auto());

            StackPanel labels = new StackPanel();
            labels.VerticalAlignment = VerticalAlignment.Center;
            labels.Children.Add(UI.Text(title, 12.3, B(Palette.Ink), FontWeights.SemiBold));
            TextBlock desc = UI.Text(description, 9.8, B(Palette.Muted));
            desc.Margin = new Thickness(0, 2, 0, 0);
            labels.Children.Add(desc);
            row.Children.Add(labels);

            Grid switchContent = new Grid();
            Border track = new Border();
            track.Width = 40;
            track.Height = 23;
            track.CornerRadius = new CornerRadius(11.5);
            track.Background = value ? B(Palette.Green) : B("#DDD5C6");
            switchContent.Children.Add(track);

            Border knob = new Border();
            knob.Width = 17;
            knob.Height = 17;
            knob.CornerRadius = new CornerRadius(8.5);
            knob.Background = B(Palette.Card);
            knob.Margin = new Thickness(3);
            knob.HorizontalAlignment = value ? HorizontalAlignment.Right : HorizontalAlignment.Left;
            knob.VerticalAlignment = VerticalAlignment.Center;
            knob.Effect = UI.Shadow(5, 1, 0.18);
            switchContent.Children.Add(knob);

            Button button = UI.FlatButton(switchContent, 12, toggle);
            button.Width = 40;
            button.Height = 23;
            button.Background = Brushes.Transparent;
            button.Margin = new Thickness(12, 0, 0, 0);
            UI.SetName(button, title + (value ? "，已开启" : "，已关闭"));
            button.ToolTip = value ? "点击关闭" : "点击开启";
            Grid.SetColumn(button, 1);
            row.Children.Add(button);
            return row;
        }

        private Button BuildSettingsAction(string label, string path, Action action, bool danger)
        {
            Grid content = new Grid();
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            content.ColumnDefinitions.Add(UI.Star());
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            Path icon = UI.IconPath(path, danger ? B(Palette.Accent) : B(Palette.InkSoft), 14, 1.5);
            icon.VerticalAlignment = VerticalAlignment.Center;
            content.Children.Add(icon);

            TextBlock text = UI.Text(label, 12, danger ? B(Palette.AccentDeep) : B(Palette.Ink),
                FontWeights.Medium);
            text.Margin = new Thickness(9, 0, 0, 0);
            Grid.SetColumn(text, 1);
            content.Children.Add(text);

            Path chevron = UI.IconPath("M7 4l6 6-6 6", B(Palette.Faint), 12, 1.4);
            chevron.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(chevron, 2);
            content.Children.Add(chevron);

            Button button = UI.FlatButton(content, 9, action);
            button.Margin = new Thickness(0, 1, 0, 1);
            button.Padding = new Thickness(8, 7, 8, 7);
            button.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            button.ToolTip = label;
            UI.SetName(button, label);
            Brush normal = Brushes.Transparent;
            Brush hover = danger ? B("#FAE8E1") : B("#F7F2E8");
            UI.Hover(button, normal, hover);
            return button;
        }

        private void ShowDuePicker(FrameworkElement target, DateTime? current, Action<DateTime?> onPick)
        {
            CloseDuePopup();
            if (target == null) return;

            Border wrap = new Border();
            wrap.Margin = new Thickness(10);
            wrap.Width = 292;
            wrap.Background = B(Palette.Card);
            wrap.CornerRadius = new CornerRadius(16);
            wrap.BorderBrush = B(Palette.Rule);
            wrap.BorderThickness = new Thickness(1);
            wrap.Padding = new Thickness(12, 11, 12, 10);
            wrap.Effect = UI.Shadow(26, 8, 0.18);

            StackPanel body = new StackPanel();

            Grid heading = new Grid();
            heading.ColumnDefinitions.Add(UI.Star());
            heading.ColumnDefinitions.Add(UI.Auto());
            TextBlock title = UI.Text("截止日期", 13.5, B(Palette.Ink), FontWeights.SemiBold);
            heading.Children.Add(title);

            if (current.HasValue)
            {
                Button clear = UI.FlatButton("清除", 6, delegate
                {
                    onPick(null);
                    CloseDuePopup();
                });
                clear.Padding = new Thickness(6, 2, 6, 2);
                clear.Foreground = B(Palette.Accent);
                clear.FontSize = 10;
                clear.FontWeight = FontWeights.SemiBold;
                clear.Background = Brushes.Transparent;
                clear.ToolTip = "清除截止日期";
                UI.SetName(clear, "清除截止日期");
                UI.Hover(clear, Brushes.Transparent, B(Palette.AccentSoft));
                Grid.SetColumn(clear, 1);
                heading.Children.Add(clear);
            }
            body.Children.Add(heading);

            WrapPanel quick = new WrapPanel();
            quick.Margin = new Thickness(0, 9, 0, 3);
            AddQuickDate(quick, "今天", DateTime.Today, current, onPick);
            AddQuickDate(quick, "明天", DateTime.Today.AddDays(1), current, onPick);
            AddQuickDate(quick, "后天", DateTime.Today.AddDays(2), current, onPick);
            AddQuickDate(quick, "下周一", NextMonday(), current, onPick);
            body.Children.Add(quick);

            Border divider = UI.Divider();
            divider.Margin = new Thickness(0, 6, 0, 7);
            body.Children.Add(divider);

            TextBlock choose = UI.Text("选择日期", 9.8, B(Palette.Faint));
            choose.Margin = new Thickness(0, 0, 0, 6);
            body.Children.Add(choose);

            WpfCalendar calendar = new WpfCalendar();
            calendar.Width = 250;
            calendar.HorizontalAlignment = HorizontalAlignment.Center;
            calendar.Background = B(Palette.Card);
            calendar.BorderThickness = new Thickness(0);
            calendar.FontFamily = UI.FontUI;
            calendar.FontSize = 10.8;
            calendar.IsTodayHighlighted = true;
            calendar.FirstDayOfWeek = DayOfWeek.Monday;
            calendar.DisplayDateStart = DateTime.Today.AddYears(-8);
            calendar.DisplayDateEnd = DateTime.Today.AddYears(10);
            calendar.DisplayDate = current.HasValue ? current.Value : DateTime.Today;
            calendar.SelectedDate = current.HasValue ? current.Value : (DateTime?)null;

            Style dayStyle = new Style(typeof(CalendarDayButton));
            dayStyle.Setters.Add(new Setter(Control.FontFamilyProperty, UI.FontUI));
            dayStyle.Setters.Add(new Setter(Control.FontSizeProperty, 10.5));
            dayStyle.Setters.Add(new Setter(Control.ForegroundProperty, B(Palette.Ink)));
            dayStyle.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
            calendar.CalendarDayButtonStyle = dayStyle;

            calendar.Resources[SystemColors.HighlightBrushKey] = B(Palette.Accent);
            calendar.Resources[SystemColors.HighlightTextBrushKey] = Brushes.White;
            calendar.Resources[SystemColors.ControlBrushKey] = B(Palette.AccentSoft);
            calendar.Resources[SystemColors.ControlTextBrushKey] = B(Palette.Ink);
            calendar.Resources[SystemColors.WindowBrushKey] = B(Palette.Card);
            calendar.Resources[SystemColors.WindowTextBrushKey] = B(Palette.Ink);
            calendar.Resources[SystemColors.GrayTextBrushKey] = B(Palette.Faint);

            calendar.SelectedDatesChanged += delegate
            {
                if (calendar.SelectedDate.HasValue)
                {
                    onPick(calendar.SelectedDate.Value.Date);
                    CloseDuePopup();
                }
            };
            body.Children.Add(calendar);

            wrap.Child = body;

            Popup popup = new Popup();
            popup.PlacementTarget = target;
            popup.Placement = PlacementMode.Bottom;
            popup.HorizontalOffset = -6;
            popup.VerticalOffset = 6;
            popup.StaysOpen = false;
            popup.AllowsTransparency = true;
            popup.PopupAnimation = PopupAnimation.Fade;
            popup.Child = wrap;
            popup.IsOpen = true;
            _duePopup = popup;
        }

        private DateTime NextMonday()
        {
            DateTime date = DateTime.Today.AddDays(1);
            while (date.DayOfWeek != DayOfWeek.Monday) date = date.AddDays(1);
            if (date <= DateTime.Today) date = date.AddDays(7);
            return date;
        }

        private void AddQuickDate(WrapPanel panel, string label, DateTime date, DateTime? current,
            Action<DateTime?> onPick)
        {
            bool selected = current.HasValue && current.Value.Date == date.Date;
            Button button = UI.FlatButton(label, 7, delegate
            {
                onPick(date);
                CloseDuePopup();
            });
            button.Padding = new Thickness(9, 4, 9, 4);
            button.Margin = new Thickness(0, 0, 5, 5);
            button.FontSize = 10.5;
            button.Background = selected ? B(Palette.AccentSoft) : B("#F8F3E9");
            button.Foreground = selected ? B(Palette.AccentDeep) : B(Palette.InkSoft);
            button.BorderBrush = selected ? UI.Alpha(Palette.Accent, 0x55) : Brushes.Transparent;
            button.BorderThickness = new Thickness(selected ? 1 : 0);
            UI.SetName(button, "截止日期：" + label);
            UI.Hover(button, button.Background, selected ? B("#F6DCD2") : B("#F2EBE0"));
            panel.Children.Add(button);
        }

        private void CloseDuePopup()
        {
            if (_duePopup != null) _duePopup.IsOpen = false;
        }

        private void ClosePopups()
        {
            CloseSettingsPopup();
            CloseDuePopup();
        }

        private void OpenEdit(TaskItem task)
        {
            if (task == null) return;
            ClosePopups();
            if (_overlayHost == null) return;

            int editPriority = task.Priority;
            DateTime? editDue = task.DueDate;

            Grid host = new Grid();

            Border dim = new Border();
            dim.CornerRadius = new CornerRadius(26);
            dim.Background = new SolidColorBrush(Color.FromArgb(0x66, 0x35, 0x2A, 0x1C));
            dim.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e)
            {
                if (e.OriginalSource == dim) CloseOverlay();
            };
            host.Children.Add(dim);

            Border sheet = new Border();
            sheet.Width = 348;
            sheet.HorizontalAlignment = HorizontalAlignment.Center;
            sheet.VerticalAlignment = VerticalAlignment.Center;
            sheet.Background = B(Palette.Card);
            sheet.CornerRadius = new CornerRadius(18);
            sheet.BorderBrush = B(Palette.Rule);
            sheet.BorderThickness = new Thickness(1);
            sheet.Padding = new Thickness(20, 18, 20, 16);
            sheet.Effect = UI.Shadow(32, 10, 0.26);
            host.Children.Add(sheet);

            StackPanel body = new StackPanel();

            Grid heading = new Grid();
            heading.ColumnDefinitions.Add(UI.Star());
            heading.ColumnDefinitions.Add(UI.Auto());
            StackPanel headingText = new StackPanel();
            headingText.Children.Add(UI.Text("编辑作业", 16, B(Palette.Ink), FontWeights.SemiBold));
            if (task.CreatedDate.Year > 1900)
            {
                TextBlock created = UI.Text("创建于 " + task.CreatedDate.ToString("M月d日 HH:mm", CultureInfo.GetCultureInfo("zh-CN")),
                    9.5, B(Palette.Faint));
                created.Margin = new Thickness(0, 2, 0, 0);
                headingText.Children.Add(created);
            }
            heading.Children.Add(headingText);

            Button close = UI.FlatButton(UI.IconPath("M6 6l8 8 M14 6l-8 8", B(Palette.Muted), 13, 1.6),
                8, CloseOverlay);
            close.Width = 28;
            close.Height = 28;
            close.Background = Brushes.Transparent;
            close.ToolTip = "关闭";
            UI.SetName(close, "关闭编辑");
            UI.SetId(close, "EditCloseButton");
            UI.Hover(close, Brushes.Transparent, B("#F3EDE2"));
            Grid.SetColumn(close, 1);
            heading.Children.Add(close);
            body.Children.Add(heading);

            body.Children.Add(BuildFieldLabel("作业内容", 14));

            Border titleWrap = new Border();
            titleWrap.Background = B("#FBF8F1");
            titleWrap.BorderBrush = B(Palette.Rule);
            titleWrap.BorderThickness = new Thickness(1);
            titleWrap.CornerRadius = new CornerRadius(10);
            titleWrap.Padding = new Thickness(10, 8, 10, 8);

            TextBox editBox = new TextBox();
            editBox.Text = task.Title;
            editBox.FontFamily = UI.FontUI;
            editBox.FontSize = 14.5;
            editBox.Foreground = B(Palette.Ink);
            editBox.Background = Brushes.Transparent;
            editBox.BorderThickness = new Thickness(0);
            editBox.Padding = new Thickness(0);
            editBox.Margin = new Thickness(0);
            editBox.Height = 48;
            editBox.MaxLength = 160;
            editBox.AcceptsReturn = false;
            editBox.TextWrapping = TextWrapping.Wrap;
            editBox.VerticalContentAlignment = VerticalAlignment.Center;
            editBox.CaretBrush = B(Palette.Accent);
            editBox.SelectionBrush = UI.Alpha(Palette.Accent, 0x66);
            editBox.FocusVisualStyle = null;
            AutomationProperties.SetName(editBox, "作业内容");
            AutomationProperties.SetAutomationId(editBox, "EditTaskInput");
            editBox.GotKeyboardFocus += delegate { titleWrap.BorderBrush = B(Palette.Accent); };
            editBox.LostKeyboardFocus += delegate { titleWrap.BorderBrush = B(Palette.Rule); };
            editBox.TextChanged += delegate
            {
                if (editBox.Text != null && editBox.Text.Trim().Length > 0)
                {
                    titleWrap.BorderBrush = B(Palette.Rule);
                }
            };
            titleWrap.Child = editBox;
            body.Children.Add(titleWrap);

            body.Children.Add(BuildFieldLabel("轻重缓急", 15));

            Grid priorityGrid = new Grid();
            int[] order = new int[] { Priority.High, Priority.Medium, Priority.Low };
            for (int i = 0; i < order.Length; i++)
            {
                priorityGrid.ColumnDefinitions.Add(UI.Star());
            }
            Action<int> choosePriority = null;
            choosePriority = delegate(int value)
            {
                editPriority = value;
                FillPriorityGrid(priorityGrid, editPriority, choosePriority);
            };
            FillPriorityGrid(priorityGrid, editPriority, choosePriority);
            body.Children.Add(priorityGrid);

            body.Children.Add(BuildFieldLabel("截止日期", 15));

            Path dueIcon = UI.IconPath("M4 6h12v10H4z M4 9h12 M7 4v3 M13 4v3",
                B(Palette.Muted), 13, 1.4);
            TextBlock dueText = UI.Text("未设置", 11.5, B(Palette.Muted), FontWeights.Medium);
            dueText.Margin = new Thickness(6, 0, 0, 0);
            StackPanel dueContent = new StackPanel();
            dueContent.Orientation = Orientation.Horizontal;
            dueContent.VerticalAlignment = VerticalAlignment.Center;
            dueContent.Children.Add(dueIcon);
            dueContent.Children.Add(dueText);

            Button dueButton = UI.FlatButton(dueContent, 10, null);
            dueButton.Height = 38;
            dueButton.Padding = new Thickness(11, 0, 11, 0);
            dueButton.HorizontalContentAlignment = HorizontalAlignment.Left;
            dueButton.Background = B("#FBF8F1");
            dueButton.BorderBrush = B(Palette.Rule);
            dueButton.BorderThickness = new Thickness(1);
            UI.SetName(dueButton, "设置截止日期");
            UI.SetId(dueButton, "EditDueButton");

            Action updateDue = delegate
            {
                if (editDue.HasValue)
                {
                    dueText.Text = DateUtil.DueLabel(editDue);
                    dueText.Foreground = B(Palette.AccentDeep);
                    dueIcon.Stroke = B(Palette.Accent);
                    dueButton.Background = B(Palette.AccentSoft);
                    dueButton.BorderBrush = UI.Alpha(Palette.Accent, 0x55);
                }
                else
                {
                    dueText.Text = "未设置";
                    dueText.Foreground = B(Palette.Muted);
                    dueIcon.Stroke = B(Palette.Muted);
                    dueButton.Background = B("#FBF8F1");
                    dueButton.BorderBrush = B(Palette.Rule);
                }
            };
            dueButton.Click += delegate
            {
                ShowDuePicker(dueButton, editDue, delegate(DateTime? date)
                {
                    editDue = date;
                    updateDue();
                });
            };
            dueButton.MouseEnter += delegate
            {
                dueButton.Background = editDue.HasValue ? B("#F6DBD1") : B("#F5EFE4");
            };
            dueButton.MouseLeave += delegate { updateDue(); };
            updateDue();
            body.Children.Add(dueButton);

            Grid actions = new Grid();
            actions.Margin = new Thickness(0, 18, 0, 0);
            actions.ColumnDefinitions.Add(UI.Star());
            actions.ColumnDefinitions.Add(UI.Auto());

            TextBlock hint = UI.Text("Enter 保存 · Esc 关闭", 9.2, B(Palette.Faint));
            hint.VerticalAlignment = VerticalAlignment.Center;
            actions.Children.Add(hint);

            StackPanel right = new StackPanel();
            right.Orientation = Orientation.Horizontal;
            right.HorizontalAlignment = HorizontalAlignment.Right;

            Button cancel = UI.FlatButton("取消", 9, CloseOverlay);
            cancel.Height = 32;
            cancel.Padding = new Thickness(12, 0, 12, 0);
            cancel.Foreground = B(Palette.Muted);
            cancel.FontSize = 11.5;
            cancel.Background = Brushes.Transparent;
            UI.SetId(cancel, "EditCancelButton");
            UI.Hover(cancel, Brushes.Transparent, B("#F2EBE0"));
            right.Children.Add(cancel);

            Button save = UI.FlatButton("保存", 9, null);
            save.Height = 32;
            save.MinWidth = 64;
            save.Padding = new Thickness(14, 0, 14, 0);
            save.Foreground = B(Colors.White);
            save.FontSize = 11.5;
            save.FontWeight = FontWeights.SemiBold;
            save.Background = B(Palette.Accent);
            save.Margin = new Thickness(6, 0, 0, 0);
            UI.Hover(save, B(Palette.Accent), B(Palette.AccentDeep));
            UI.SetName(save, "保存修改");
            UI.SetId(save, "EditSaveButton");
            right.Children.Add(save);
            Grid.SetColumn(right, 1);
            actions.Children.Add(right);
            body.Children.Add(actions);

            Action saveAction = delegate
            {
                string text = editBox.Text == null ? "" : editBox.Text.Trim();
                if (text.Length == 0)
                {
                    titleWrap.BorderBrush = B(Palette.Accent);
                    editBox.Focus();
                    return;
                }
                task.Title = text;
                task.Priority = Priority.Clamp(editPriority);
                task.Due = editDue.HasValue ? DateUtil.Key(editDue.Value) : null;
                CloseOverlay();
                Persist();
                RefreshUI(true, task.Id);
            };
            save.Click += delegate { saveAction(); };
            editBox.PreviewKeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None)
                {
                    e.Handled = true;
                    saveAction();
                    return;
                }
                if (e.Key == Key.Escape)
                {
                    e.Handled = true;
                    CloseOverlay();
                }
            };

            sheet.Child = body;

            _overlayHost.Children.Add(host);
            _overlayHost.Opacity = 0;
            _overlayHost.Visibility = Visibility.Visible;
            _overlayHost.BeginAnimation(UIElement.OpacityProperty,
                new DoubleAnimation(0, 1, Ms(160)) { EasingFunction = EaseOut() });

            sheet.RenderTransformOrigin = new Point(0.5, 0.5);
            ScaleTransform scale = new ScaleTransform(0.97, 0.97);
            sheet.RenderTransform = scale;
            DoubleAnimation grow = new DoubleAnimation(0.97, 1, Ms(200));
            grow.EasingFunction = EaseOut();
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, grow);

            Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(delegate
            {
                editBox.Focus();
                editBox.SelectAll();
            }));
        }

        private void FillPriorityGrid(Grid grid, int selectedPriority, Action<int> choose)
        {
            if (grid == null) return;
            grid.Children.Clear();
            int[] order = new int[] { Priority.High, Priority.Medium, Priority.Low };
            for (int i = 0; i < order.Length; i++)
            {
                int value = order[i];
                Button segment = BuildPriorityChip(value, selectedPriority == value, true, null,
                    "EditPriority_");
                segment.Margin = new Thickness(i == 0 ? 0 : 2, 0, i == order.Length - 1 ? 0 : 2, 0);
                segment.Click += delegate
                {
                    if (choose != null) choose(value);
                };
                Grid.SetColumn(segment, i);
                grid.Children.Add(segment);
            }
        }

        private TextBlock BuildFieldLabel(string text, double topMargin)
        {
            TextBlock label = UI.Text(text, 10.2, B(Palette.Muted), FontWeights.SemiBold);
            label.Margin = new Thickness(0, topMargin, 0, 6);
            return label;
        }

        private void CloseOverlay()
        {
            if (_overlayHost == null || _overlayHost.Visibility != Visibility.Visible) return;
            CloseDuePopup();
            _overlayHost.BeginAnimation(UIElement.OpacityProperty, null);
            _overlayHost.Visibility = Visibility.Collapsed;
            _overlayHost.Children.Clear();
            if (_addBox != null) _addBox.Focus();
        }

        private Border BuildToast()
        {
            Border toast = new Border();
            toast.Visibility = Visibility.Collapsed;
            toast.Opacity = 0;
            toast.Background = B(Palette.Toast);
            toast.CornerRadius = new CornerRadius(13);
            toast.Padding = new Thickness(12, 9, 9, 9);
            toast.HorizontalAlignment = HorizontalAlignment.Stretch;
            toast.VerticalAlignment = VerticalAlignment.Bottom;
            toast.Margin = new Thickness(22, 0, 22, 18);
            toast.Effect = UI.Shadow(18, 4, 0.22);
            toast.RenderTransform = new TranslateTransform(0, 0);

            Grid content = new Grid();
            content.ColumnDefinitions.Add(UI.Star());
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _toastText = UI.Text("", 11.2, B("#FFF8EF"));
            _toastText.TextWrapping = TextWrapping.Wrap;
            _toastText.LineHeight = 16;
            _toastText.VerticalAlignment = VerticalAlignment.Center;
            _toastText.Margin = new Thickness(0, 0, 8, 0);
            content.Children.Add(_toastText);

            _toastUndo = UI.FlatButton("撤销", 7, OnToastUndo);
            _toastUndo.Padding = new Thickness(7, 4, 7, 4);
            _toastUndo.Foreground = B("#FFB7A2");
            _toastUndo.FontSize = 10.8;
            _toastUndo.FontWeight = FontWeights.SemiBold;
            _toastUndo.Visibility = Visibility.Collapsed;
            _toastUndo.Background = Brushes.Transparent;
            UI.SetName(_toastUndo, "撤销");
            UI.Hover(_toastUndo, Brushes.Transparent, UI.Alpha(Colors.White, 0x1A));
            Grid.SetColumn(_toastUndo, 1);
            content.Children.Add(_toastUndo);

            toast.Child = content;
            return toast;
        }

        private void ShowToast(string message, string actionLabel, Action action, int durationMs)
        {
            if (_toast == null) return;
            _toastVersion++;
            int version = _toastVersion;
            _undoAction = action;
            bool hasUndo = action != null && !string.IsNullOrEmpty(actionLabel);
            _toastText.Text = message;
            if (hasUndo)
            {
                _toastUndo.Content = actionLabel;
                _toastUndo.Visibility = Visibility.Visible;
            }
            else
            {
                _toastUndo.Visibility = Visibility.Collapsed;
            }

            _toast.Visibility = Visibility.Visible;
            _toast.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, Ms(150)));
            TranslateTransform move = _toast.RenderTransform as TranslateTransform;
            if (move != null)
            {
                DoubleAnimation slide = new DoubleAnimation(10, 0, Ms(180));
                slide.EasingFunction = EaseOut();
                move.BeginAnimation(TranslateTransform.YProperty, slide);
            }

            if (_toastTimer == null)
            {
                _toastTimer = new DispatcherTimer();
                _toastTimer.Tick += delegate
                {
                    _toastTimer.Stop();
                    HideToast(version);
                };
            }
            _toastTimer.Stop();
            _toastTimer.Interval = TimeSpan.FromMilliseconds(durationMs);
            _toastTimer.Start();
        }

        private void HideToast(int version)
        {
            if (_toast == null || version != _toastVersion) return;
            DoubleAnimation fade = new DoubleAnimation(1, 0, Ms(150));
            fade.Completed += delegate
            {
                if (_toast != null && version == _toastVersion)
                {
                    _toast.Visibility = Visibility.Collapsed;
                }
            };
            _toast.BeginAnimation(UIElement.OpacityProperty, fade);
        }

        private void OnToastUndo()
        {
            Action action = _undoAction;
            _undoAction = null;
            _toastVersion++;
            if (_toastTimer != null) _toastTimer.Stop();
            if (_toast != null) _toast.Visibility = Visibility.Collapsed;
            if (_toastUndo != null) _toastUndo.Visibility = Visibility.Collapsed;
            if (action != null) action();
        }

        private static SolidColorBrush B(Color color)
        {
            return UI.B(color);
        }

        private static SolidColorBrush B(string hex)
        {
            return UI.B(hex);
        }
    }
}

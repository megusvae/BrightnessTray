/*
	This file is part of BrightnessTray.

    BrightnessTray is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    BrightnessTray is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with BrightnessTray.  If not, see <http://www.gnu.org/licenses/>.

*/
namespace BrightnessTray
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Input;
    using System.Windows.Interop;
    using System.Windows.Media;
    using Microsoft.Win32;

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        /// <summary>
        /// Initializes a new instance of the MainWindow class.
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
            
            // initialise flags
            isKeyDown = false;
            MouseClickToHideNotifyIcon = false;

            PreferenceEventHandler = null;
            PreferenceEvent = null;

            // Initialize brightness controller
            BrightnessController.Initialize();

            // set up listener for brightness changed events
            eventWatcher = new BrightnessWatcher();
            eventWatcher.BrightnessChanged += EventWatcher_BrightnessChanged;

            // set up Windows10 1903 taskbar theme change events
            registryWatcher = new RegistryWatcher();
            registryWatcher.OnUpdateStatus += RegistryWatcher_OnUpdateStatus;

            CreateNotifyIcon();

            this.Visibility = Visibility.Hidden;

            if (!Config.showPercentageText)
            {
                // Hide individual percentage labels if configured
            }
        }

        private void RegistryWatcher_OnUpdateStatus(object sender, string e)
        {
            RefreshAllMonitorDisplays();
        }

        /// <summary>
        /// Update the sliders due to a brightness changed event.
        /// </summary>
        private void EventWatcher_BrightnessChanged(object sender, BrightnessWatcher.BrightnessChangedEventArgs e)
        {
            // Update only if not currently dragging
            if (!IsMouseOver && !isKeyDown)
            {
                RefreshAllMonitorDisplays();
            }
        }

        /// <summary>
        /// Refreshes the display of all monitor brightness sliders.
        /// </summary>
        private void RefreshAllMonitorDisplays()
        {
            this.Dispatcher.BeginInvoke((Action)(() =>
            {
                var monitors = BrightnessController.GetMonitors();
                
                foreach (var monitor in monitors)
                {
                    int brightness = BrightnessController.GetBrightness(monitor);
                    if (brightness >= 0)
                    {
                        UpdateMonitorSlider(monitor, brightness);
                    }
                }

                // Update tray icon based on primary monitor
                if (monitors.Count > 0)
                {
                    int primaryBrightness = BrightnessController.GetBrightness(monitors[0]);
                    if (primaryBrightness >= 0)
                    {
                        NotifyIcon.Text = "Brightness " + primaryBrightness.ToString() + "%";
                        DrawIcon.updateNotifyIcon(NotifyIcon, primaryBrightness);
                    }
                }
            }));
        }

        /// <summary>
        /// Updates a specific monitor's slider UI.
        /// </summary>
        private void UpdateMonitorSlider(MonitorInfo monitor, int brightnessValue)
        {
            if (brightnessValue < 0 || brightnessValue > 100)
                return;

            string monitorTag = $"slider_{monitor.Handle}";
            var slider = FindMonitorSlider(monitorTag);
            
            if (slider != null)
            {
                ignoreValueChanged = true;
                slider.Value = brightnessValue;
                ignoreValueChanged = false;

                // Update label if visible
                var label = FindMonitorLabel(monitorTag);
                if (label != null && Config.showPercentageText)
                {
                    label.Content = brightnessValue.ToString() + "%";
                }
            }
        }

        /// <summary>
        /// Finds a slider for a specific monitor.
        /// </summary>
        private Slider FindMonitorSlider(string tag)
        {
            foreach (UIElement element in MonitorSlidersPanel.Children)
            {
                if (element is Border border)
                {
                    var slider = border.Child as Slider;
                    if (slider != null && slider.Tag?.ToString() == tag)
                        return slider;
                }
            }
            return null;
        }

        /// <summary>
        /// Finds a label for a specific monitor.
        /// </summary>
        private Label FindMonitorLabel(string tag)
        {
            foreach (UIElement element in MonitorSlidersPanel.Children)
            {
                if (element is StackPanel stack)
                {
                    foreach (UIElement child in stack.Children)
                    {
                        if (child is Label label && label.Tag?.ToString() == tag + "_label")
                            return label;
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Is the keyboard being pressed?
        /// </summary>
        private bool isKeyDown;

        /// <summary>
        /// Listener to backlight change events from WMI.
        /// </summary>
        private BrightnessWatcher eventWatcher;

        /// <summary>
        /// Listener to registry change events.
        /// </summary>
        private RegistryWatcher registryWatcher;

        /// <summary>
        /// Delegate for handling user preference changes.
        /// </summary>
        private delegate void UserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e);

        /// <summary>
        /// User preferences changed event handler.
        /// </summary>
        private event UserPreferenceChanged PreferenceEvent;

        /// <summary>
        /// Gets or sets a handler for user preference changes.
        /// </summary>
        private UserPreferenceChangedEventHandler PreferenceEventHandler { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether or not we are currently blocking sleep mode 'Caffeine'.
        /// </summary>
        private bool CaffeineEnabled { get; set; }

        /// <summary>
        /// Gets or sets the window's notify icon.
        /// </summary>
        private System.Windows.Forms.NotifyIcon NotifyIcon { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the user hid the window by clicking the notify icon.
        /// </summary>
        private bool MouseClickToHideNotifyIcon { get; set; }

        /// <summary>
        /// Gets or sets the location of the cursor when the window was last hidden by clicking the notify icon.
        /// </summary>
        private Point MouseClickToHideNotifyIconPoint { get; set; }

        /// <summary>
        /// The text label in the application's tray icon context menu.
        /// </summary>
        private System.Windows.Forms.MenuItem mnuLabel;

        /// <summary>
        /// The autostart menu item.
        /// </summary>
        private System.Windows.Forms.MenuItem mnuAutostart;

        /// <summary>
        /// Don't trigger a brightness change action if the value update itself was from external source.
        /// </summary>
        private bool ignoreValueChanged = false;

        /// <summary>
        /// Updates the display (position and appearance) of the window if it is currently visible.
        /// </summary>
        public void UpdateWindowDisplayIfOpen(bool activatewindow)
        {
            if (this.Visibility == Visibility.Visible)
                this.UpdateWindowDisplay(activatewindow);
        }

        /// <summary>
        /// Updates the display (position and appearance) of the window.
        /// </summary>
        public void UpdateWindowDisplay(bool activatewindow)
        {
            if (this.IsLoaded)
            {
                // set handlers if necessary
                this.SetHandlers();

                // get the handle of the window
                HwndSource windowhandlesource = PresentationSource.FromVisual(this) as HwndSource;

                bool glassenabled = Compatibility.IsDWMEnabled;

                Rect windowbounds = (glassenabled ? WindowPositioning.GetWindowSize(windowhandlesource.Handle) : WindowPositioning.GetWindowClientAreaSize(windowhandlesource.Handle));

                // work out the current screen's DPI
                Matrix screenmatrix = windowhandlesource.CompositionTarget.TransformToDevice;

                double dpiX = screenmatrix.M11;
                double dpiY = screenmatrix.M22;

                Point position = WindowPositioning.GetWindowPosition(this.NotifyIcon, windowbounds.Width, windowbounds.Height, dpiX);

                // translate wpf points to screen coordinates
                Point screenposition = new Point(position.X / dpiX, position.Y / dpiY);

                this.Left = screenposition.X;
                this.Top = screenposition.Y;

                // update borders
                if (glassenabled)
                    this.Style = (Style)FindResource("AeroBorderStyle");
                else
                    this.SetNonGlassBorder(this.IsActive);

                // fix aero border if necessary
                if (glassenabled)
                {
                    WindowBorder.Margin = new Thickness(1 / dpiX);
                    this.BorderThickness = new Thickness(0);

                    windowhandlesource.CompositionTarget.BackgroundColor = Colors.Transparent;

                    int xmargin = Convert.ToInt32(1);
                    int ymargin = Convert.ToInt32(1);

                    NativeMethods.MARGINS margins = new NativeMethods.MARGINS() { cxLeftWidth = xmargin, cxRightWidth = xmargin, cyBottomHeight = ymargin, cyTopHeight = ymargin };

                    NativeMethods.DwmExtendFrameIntoClientArea(windowhandlesource.Handle, ref margins);
                }
                else
                {
                    WindowBorder.Margin = new Thickness(0);
                    this.BorderThickness = new Thickness(1 / dpiX);
                }

                if (activatewindow)
                {
                    this.Show();
                    this.Activate();
                }
            }
        }

        #region Event Handlers

        /// <summary>
        /// Custom DefWindowProc for handling window messages.
        /// </summary>
        private IntPtr WndProc(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (this.IsLoaded && this.Visibility == Visibility.Visible)
            {
                switch (msg)
                {
                    case NativeMethods.WM_NCHITTEST:
                        if (!NativeMethods.IsOverClientArea(hWnd, wParam, lParam))
                            handled = true;
                        break;

                    case NativeMethods.WM_SETCURSOR:
                        if (!NativeMethods.IsOverClientArea(hWnd, wParam, lParam))
                        {
                            int hiword = (int)lParam >> 16;
                            if (hiword == NativeMethods.WM_LBUTTONDOWN
                                || hiword == NativeMethods.WM_RBUTTONDOWN
                                || hiword == NativeMethods.WM_MBUTTONDOWN
                                || hiword == NativeMethods.WM_XBUTTONDOWN)
                            {
                                handled = true;
                                this.Focus();
                            }
                        }
                        break;

                    case NativeMethods.WM_DWMCOMPOSITIONCHANGED:
                        this.UpdateWindowDisplayIfOpen(false);
                        break;

                    case NativeMethods.WM_SIZE:
                        this.UpdateWindowDisplayIfOpen(false);
                        break;
                }
            }

            return IntPtr.Zero;
        }

        /// <summary>
        /// Sets handlers for notifying the application of desktop preference changes.
        /// </summary>
        private void SetHandlers()
        {
            if (this.PreferenceEvent == null && this.PreferenceEventHandler == null)
            {
                this.PreferenceEvent = new UserPreferenceChanged(this.DesktopPreferenceChangedHandler);
                this.PreferenceEventHandler = new UserPreferenceChangedEventHandler(this.PreferenceEvent);
                SystemEvents.UserPreferenceChanged += this.PreferenceEventHandler;
            }
        }

        /// <summary>
        /// Releases handlers set by SetHandlers().
        /// </summary>
        private void ReleaseHandlers()
        {
            if (this.PreferenceEvent != null || this.PreferenceEventHandler != null)
            {
                SystemEvents.UserPreferenceChanged -= this.PreferenceEventHandler;
                this.PreferenceEvent = null;
                this.PreferenceEventHandler = null;
            }
        }

        /// <summary>
        /// Handler for UserPreferenceChangedEventArgs.
        /// </summary>
        private void DesktopPreferenceChangedHandler(object sender, UserPreferenceChangedEventArgs e)
        {
            if (e.Category == UserPreferenceCategory.Desktop)
                this.UpdateWindowDisplayIfOpen(false);
        }

        #endregion

        /// <summary>
        /// Notify icon clicked or double-clicked method.
        /// </summary>
        private void NotifyIconClick(object sender, System.Windows.Forms.MouseEventArgs args)
        {
            if (args.Button == System.Windows.Forms.MouseButtons.Left &&
                (!this.MouseClickToHideNotifyIcon
                || (WindowPositioning.GetCursorPosition().X != this.MouseClickToHideNotifyIconPoint.X || WindowPositioning.GetCursorPosition().Y != this.MouseClickToHideNotifyIconPoint.Y)))
            {
                if (!this.IsLoaded)
                {
                    this.Show();
                }
                this.UpdateWindowDisplay(true);
            }
            else
            {
                this.MouseClickToHideNotifyIcon = false;
            }
        }

        /// <summary>
        /// Sets the border of the window when the DWM is not enabled.
        /// </summary>
        private void SetNonGlassBorder(bool windowactivated)
        {
            // Border styling handled by WPF styles
        }

        /// <summary>
        /// Window deactivated method. Hides window if not pinned.
        /// </summary>
        private void Window_Deactivated(object sender, EventArgs e)
        {
            this.HideWindow();
        }

        /// <summary>
        /// Hides the window.
        /// </summary>
        private void HideWindow()
        {
            this.MouseClickToHideNotifyIcon = WindowPositioning.IsCursorOverNotifyIcon(this.NotifyIcon) && WindowPositioning.IsNotificationAreaActive;
            if (this.MouseClickToHideNotifyIcon)
                this.MouseClickToHideNotifyIconPoint = WindowPositioning.GetCursorPosition();

            this.ReleaseHandlers();
            this.Visibility = Visibility.Hidden;
        }

        /// <summary>
        /// Window closing method.
        /// </summary>
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            eventWatcher.Dispose();

            this.NotifyIcon.Visible = false;
            this.NotifyIcon.Dispose();

            if (this.IsLoaded)
            {
                HwndSource source = PresentationSource.FromVisual(this) as HwndSource;
                source.RemoveHook(this.WndProc);
            }

            this.ReleaseHandlers();
        }

        /// <summary>
        /// Exit button clicked method.
        /// </summary>
        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            this.Exit();
        }

        /// <summary>
        /// Sleep menu button clicked method.
        /// </summary>
        private void SleepMenuEventHandler(object sender, EventArgs e)
        {
            System.Windows.Forms.Application.SetSuspendState(System.Windows.Forms.PowerState.Suspend, true, true);
        }

        /// <summary>
        /// Exit menu button (notify icon) clicked method.
        /// </summary>
        private void ExitMenuEventHandler(object sender, EventArgs e)
        {
            this.Exit();
        }

        /// <summary>
        /// Close the application.
        /// </summary>
        private void Exit()
        {
            Application.Current.Shutdown();
        }

        /// <summary>
        /// Window activated method.
        /// </summary>
        private void Window_Activated(object sender, EventArgs e)
        {
            RefreshAllMonitorDisplays();

            if (!Compatibility.IsDWMEnabled)
            {
                this.SetNonGlassBorder(true);
            }
        }

        /// <summary>
        /// Window loaded method.
        /// </summary>
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            this.UpdateWindowDisplay(false);
            CreateMonitorSliders();

            HwndSource source = PresentationSource.FromVisual(this) as HwndSource;
            source.AddHook(this.WndProc);
        }

        /// <summary>
        /// Creates UI sliders for all detected monitors.
        /// </summary>
        private void CreateMonitorSliders()
        {
            MonitorSlidersPanel.Children.Clear();
            var monitors = BrightnessController.GetMonitors();

            foreach (var monitor in monitors)
            {
                var monitorGroup = CreateMonitorSliderGroup(monitor);
                MonitorSlidersPanel.Children.Add(monitorGroup);
            }
        }

        /// <summary>
        /// Creates a slider group (label + slider) for a single monitor.
        /// </summary>
        private Border CreateMonitorSliderGroup(MonitorInfo monitor)
        {
            var stackPanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            // Monitor name label
            var nameLabel = new Label
            {
                Content = monitor.ToString(),
                HorizontalAlignment = HorizontalAlignment.Center,
                Padding = new Thickness(0, 0, 0, 5),
                FontSize = 11,
                Foreground = SystemColors.ControlTextBrush
            };
            stackPanel.Children.Add(nameLabel);

            // Brightness percentage label
            var percentageLabel = new Label
            {
                Content = monitor.CurrentBrightness.ToString() + "%",
                HorizontalAlignment = HorizontalAlignment.Center,
                Padding = new Thickness(0),
                FontSize = 10,
                Tag = $"slider_{monitor.Handle}_label",
                Visibility = Config.showPercentageText ? Visibility.Visible : Visibility.Collapsed
            };
            stackPanel.Children.Add(percentageLabel);

            // Brightness slider
            var slider = new Slider
            {
                Orientation = Orientation.Vertical,
                Height = 150,
                HorizontalAlignment = HorizontalAlignment.Center,
                TickPlacement = TickPlacement.Both,
                TickFrequency = 10,
                Maximum = 100,
                Minimum = 0,
                Value = monitor.CurrentBrightness,
                Tag = $"slider_{monitor.Handle}",
                Margin = new Thickness(0, 5, 0, 5)
            };

            slider.Style = (Style)FindResource("SliderStyle") ?? CreateDefaultSliderStyle();
            slider.ValueChanged += (sender, e) => Slider_ValueChanged(sender, e, monitor);

            stackPanel.Children.Add(slider);

            // Wrap in border for visual separation
            var border = new Border
            {
                Child = stackPanel,
                Style = (Style)FindResource("MonitorGroupStyle")
            };

            return border;
        }

        /// <summary>
        /// Creates a default slider style if one is not defined in resources.
        /// </summary>
        private Style CreateDefaultSliderStyle()
        {
            var style = new Style(typeof(Slider));
            style.Setters.Add(new Setter(Slider.FocusVisualStyleProperty, null));
            return style;
        }

        /// <summary>
        /// Creates notify icon and menu.
        /// </summary>
        private void CreateNotifyIcon()
        {
            System.Windows.Forms.NotifyIcon notifyicon;

            notifyicon = new System.Windows.Forms.NotifyIcon();

            System.Windows.Forms.MenuItem mnuMonitorOff = new System.Windows.Forms.MenuItem("Power off display", new EventHandler(this.MonitorOffMenuEventHandler));
            System.Windows.Forms.MenuItem mnuScreenSaver = new System.Windows.Forms.MenuItem("Start screen saver", new EventHandler(this.StartScreenSaverMenuEventHandler));
            System.Windows.Forms.MenuItem mnuSleep = new System.Windows.Forms.MenuItem("Enter sleep mode", new EventHandler(this.SleepMenuEventHandler));
            System.Windows.Forms.MenuItem mnuCaffeine = new System.Windows.Forms.MenuItem("Caffeine", new EventHandler(this.CaffeineMenuEventHandler));
            mnuAutostart = new System.Windows.Forms.MenuItem("Autostart", new EventHandler(this.AutostartMenuEventHandler));
            mnuAutostart.Checked = Autostart.CheckStartupFolderShortcutsExists();

            System.Windows.Forms.MenuItem mnuExit = new System.Windows.Forms.MenuItem("Close", new EventHandler(this.ExitMenuEventHandler));
            mnuLabel = new System.Windows.Forms.MenuItem("");
            mnuLabel.Enabled = false;

            System.Windows.Forms.MenuItem[] menuitems = new System.Windows.Forms.MenuItem[]
            {
                mnuLabel, new System.Windows.Forms.MenuItem("-"), mnuMonitorOff, mnuScreenSaver, mnuSleep, new System.Windows.Forms.MenuItem("-"), mnuCaffeine, mnuAutostart, new System.Windows.Forms.MenuItem("-"), mnuExit
            };

            System.Windows.Forms.ContextMenu contextmenu = new System.Windows.Forms.ContextMenu(menuitems);
            contextmenu.Popup += this.ContextMenuPopup;

            notifyicon.ContextMenu = contextmenu;

            notifyicon.MouseClick += this.NotifyIconClick;
            notifyicon.MouseDoubleClick += this.NotifyIconClick;

            notifyicon.Visible = true;

            var monitors = BrightnessController.GetMonitors();
            if (monitors.Count > 0)
            {
                int primaryBrightness = BrightnessController.GetBrightness(monitors[0]);
                if (primaryBrightness >= 0)
                {
                    DrawIcon.updateNotifyIcon(notifyicon, primaryBrightness);
                }
            }

            this.NotifyIcon = notifyicon;
        }

        private void AutostartMenuEventHandler(object sender, EventArgs e)
        {
            if (mnuAutostart.Checked)
            {
                Autostart.DeleteStartupFolderShortcut();
            }
            else
            {
                Autostart.CreateStartupFolderShortcut();
            }
            mnuAutostart.Checked = Autostart.CheckStartupFolderShortcutsExists();
        }

        private void ContextMenuPopup(object sender, EventArgs e)
        {
            var monitors = BrightnessController.GetMonitors();
            if (monitors.Count > 0)
            {
                int primaryBrightness = BrightnessController.GetBrightness(monitors[0]);
                if (primaryBrightness >= 0)
                {
                    mnuLabel.Text = "Brightness: " + primaryBrightness + "%";
                }
            }
        }

        private void CaffeineMenuEventHandler(object sender, EventArgs e)
        {
            System.Windows.Forms.MenuItem menuitem = sender as System.Windows.Forms.MenuItem;
            if (menuitem != null)
            {
                if (menuitem.Checked)
                {
                    Caffeine.unlockSleepMode();
                }
                else
                {
                    Caffeine.lockSleepMode();
                }

                menuitem.Checked = !menuitem.Checked;
            }
        }

        private void MonitorOffMenuEventHandler(object sender, EventArgs e)
        {
            MonitorOff.TurnOffMonitor(this);
        }

        private void StartScreenSaverMenuEventHandler(object sender, EventArgs e)
        {
            MonitorOff.StartScreenSaver(this);
        }

        /// <summary>
        /// Hyperlink clicked method - turn off monitor.
        /// </summary>
        private void Hyperlink_Click(object sender, RoutedEventArgs e)
        {
            this.HideWindow();
            MonitorOff.TurnOffMonitor(this);
        }

        /// <summary>
        /// Hyperlink clicked method - enter sleep mode.
        /// </summary>
        private void SleepHyperlink_Click(object sender, RoutedEventArgs e)
        {
            this.HideWindow();
            System.Windows.Forms.Application.SetSuspendState(System.Windows.Forms.PowerState.Suspend, true, true);
        }

        /// <summary>
        /// Change the monitor brightness when a slider value changes.
        /// </summary>
        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e, MonitorInfo monitor)
        {
            if (this.Visibility != Visibility.Visible)
                return;

            if (ignoreValueChanged)
                return;

            var slider = sender as Slider;
            if (slider == null)
                return;

            var newBrightness = (int)e.NewValue;

            // Update label
            var label = FindMonitorLabel($"slider_{monitor.Handle}");
            if (label != null && Config.showPercentageText)
            {
                label.Content = newBrightness.ToString() + "%";
            }

            // Change the brightness in a background thread to avoid UI blocking
            new Thread((data) =>
            {
                BrightnessController.SetBrightness(monitor, newBrightness);

            }).Start();

            // Update tray icon if this is the primary monitor
            var monitors = BrightnessController.GetMonitors();
            if (monitors.Count > 0 && monitor == monitors[0])
            {
                NotifyIcon.Text = "Brightness " + newBrightness.ToString() + "%";
                DrawIcon.updateNotifyIcon(NotifyIcon, newBrightness);
            }
        }

        private void Window_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            // Focus the first slider if available
            foreach (UIElement element in MonitorSlidersPanel.Children)
            {
                if (element is Border border)
                {
                    var slider = border.Child as Slider;
                    if (slider != null)
                    {
                        slider.Focus();
                        return;
                    }
                }
            }
        }

        private void Window_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            // Find the focused slider
            Slider focusedSlider = null;
            foreach (UIElement element in MonitorSlidersPanel.Children)
            {
                if (element is Border border)
                {
                    var slider = border.Child as Slider;
                    if (slider != null && slider.IsMouseOver)
                    {
                        focusedSlider = slider;
                        break;
                    }
                }
            }

            if (focusedSlider != null)
            {
                focusedSlider.Value += e.Delta / 60;
                e.Handled = true;
            }
        }

        private void Window_KeyUp(object sender, KeyEventArgs e)
        {
            isKeyDown = false;
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            isKeyDown = true;
        }
    }
}

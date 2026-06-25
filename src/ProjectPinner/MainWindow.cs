using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;

namespace ProjectPinner
{
    internal sealed class MainWindow : Window
    {
        private readonly Config _cfg;

        private TextBox _pathBox, _nameBox, _hubNameBox;
        private TextBlock _previewText, _statusText, _storagePathText, _emptyHint;
        private Button _browseButton, _createButton, _openButton, _repinButton, _removeButton,
                       _refreshButton, _selfTestButton, _shellMenuButton,
                       _renameButton, _openStorageButton, _settingsToggle, _uninstallButton,
                       _themeAutoButton, _themeLightButton, _themeDarkButton;
        private CheckBox _pinCheck;
        private ListBox _projectsList;
        private Border _settingsPanel;
        private bool _settingsOpen;

        public MainWindow(Config cfg)
        {
            _cfg = cfg;
            ProjectsHubService.HubFolderName =
                string.IsNullOrWhiteSpace(cfg.HubFolderName) ? "Projects" : cfg.HubFolderName;

            Title = AppPaths.AppName;
            Width = 740; Height = 500; MinWidth = 660; MinHeight = 430;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Icon = AppIcon.Get();

            // Ensure the hub folder exists (with its custom icon) and clear out any legacy
            // wrongly-named pins, once, before the list is first built. Must run at startup so
            // existing installs pick up the hub icon on first launch of a new build.
            try { ProjectsHubService.EnsureHub(); } catch { }
            int cleared = 0;
            try { cleared = ProjectService.CleanupLegacyPins(); } catch { }

            LoadThemedContent();

            if (cleared > 0)
                Status("Cleaned up " + cleared + " old wrongly-named pin(s) from a previous version.");
        }

        /// <summary>
        /// Parses the XAML with the active theme's palette substituted in, then (re)binds and wires
        /// everything. Called once at startup and again whenever the user switches theme — rebuilding
        /// the content is the simplest robust way to re-skin a string-parsed XAML tree.
        /// </summary>
        private void LoadThemedContent()
        {
            // Preserve whatever the user was reading in the status line across a re-skin (the rebuilt
            // tree starts blank and RefreshList would otherwise reset it to the project count).
            string prevStatus = _statusText?.Text;

            Background = Theme.WindowBrush();

            var content = (FrameworkElement)XamlReader.Parse(Theme.Apply(Xaml));
            Content = content;

            BindControls(content);
            WireEvents();

            // Restore the open/closed state of the settings panel across a theme reload.
            _settingsPanel.Visibility = _settingsOpen ? Visibility.Visible : Visibility.Collapsed;
            _settingsToggle.Content = (_settingsOpen ? "▾  " : "▸  ") + "Settings";

            UpdatePreview();
            RefreshStorageInfo();
            RefreshList();
            UpdateReadiness();
            UpdateThemeButtons();

            if (!string.IsNullOrEmpty(prevStatus)) Status(prevStatus);
        }

        private T Find<T>(FrameworkElement root, string name) where T : FrameworkElement
        {
            // A null here means a XAML x:Name was renamed/removed; fail with the name rather than a
            // bare NullReferenceException later when the field is first used.
            if (!(root.FindName(name) is T el))
                throw new InvalidOperationException("XAML control not found: " + name);
            return el;
        }

        private void BindControls(FrameworkElement root)
        {
            _pathBox = Find<TextBox>(root, "PathBox");
            _nameBox = Find<TextBox>(root, "NameBox");
            _hubNameBox = Find<TextBox>(root, "HubNameBox");
            _previewText = Find<TextBlock>(root, "PreviewText");
            _statusText = Find<TextBlock>(root, "StatusText");
            _storagePathText = Find<TextBlock>(root, "StoragePathText");
            _emptyHint = Find<TextBlock>(root, "EmptyHint");
            _browseButton = Find<Button>(root, "BrowseButton");
            _createButton = Find<Button>(root, "CreateButton");
            _openButton = Find<Button>(root, "OpenButton");
            _repinButton = Find<Button>(root, "RepinButton");
            _removeButton = Find<Button>(root, "RemoveButton");
            _refreshButton = Find<Button>(root, "RefreshButton");
            _selfTestButton = Find<Button>(root, "SelfTestButton");
            _shellMenuButton = Find<Button>(root, "ShellMenuButton");
            _renameButton = Find<Button>(root, "RenameButton");
            _openStorageButton = Find<Button>(root, "OpenStorageButton");
            _settingsToggle = Find<Button>(root, "SettingsToggle");
            _uninstallButton = Find<Button>(root, "UninstallButton");
            _themeAutoButton = Find<Button>(root, "ThemeAutoButton");
            _themeLightButton = Find<Button>(root, "ThemeLightButton");
            _themeDarkButton = Find<Button>(root, "ThemeDarkButton");
            _pinCheck = Find<CheckBox>(root, "PinCheck");
            _projectsList = Find<ListBox>(root, "ProjectsList");
            _settingsPanel = Find<Border>(root, "SettingsPanel");
            _pinCheck.IsChecked = _cfg.AutoPin;
        }

        private void WireEvents()
        {
            _browseButton.Click += (s, e) => OnBrowse();
            _pathBox.TextChanged += (s, e) => UpdatePreview();
            _nameBox.TextChanged += (s, e) => UpdatePreview();
            _pathBox.KeyDown += OnAddFormKeyDown;
            _nameBox.KeyDown += OnAddFormKeyDown;
            _createButton.Click += (s, e) => OnCreate();
            _projectsList.SelectionChanged += (s, e) => UpdateSelectionButtons();
            _openButton.Click += (s, e) => OnOpen();
            _repinButton.Click += (s, e) => OnRepin();
            _removeButton.Click += (s, e) => OnRemove();
            _refreshButton.Click += (s, e) => RefreshList();
            _selfTestButton.Click += (s, e) => OnSelfTest();
            _shellMenuButton.Click += (s, e) => OnToggleShellMenu();
            _settingsToggle.Click += (s, e) => OnToggleSettings();
            _renameButton.Click += (s, e) => OnRename();
            _openStorageButton.Click += (s, e) => OnOpenStorage();
            _uninstallButton.Click += (s, e) => OnUninstall();
            _themeAutoButton.Click += (s, e) => OnSetTheme("auto");
            _themeLightButton.Click += (s, e) => OnSetTheme("light");
            _themeDarkButton.Click += (s, e) => OnSetTheme("dark");
            _pinCheck.Checked += (s, e) => { _cfg.AutoPin = true; _cfg.Save(); };
            _pinCheck.Unchecked += (s, e) => { _cfg.AutoPin = false; _cfg.Save(); };
        }

        private void OnAddFormKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) OnCreate();
        }

        // ---- Titlebar (matches the active theme) -------------------------------
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            ApplyDarkTitlebar();
        }

        /// <summary>Sets the immersive titlebar to dark or light to match the active theme. Safe to
        /// call before the window has a handle (it simply no-ops) and again after a theme switch.</summary>
        private void ApplyDarkTitlebar()
        {
            try
            {
                var hwnd = new WindowInteropHelper(this).Handle;
                if (hwnd == IntPtr.Zero) return;
                int on = Theme.IsDark ? 1 : 0;
                if (NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE, ref on, sizeof(int)) != 0)
                    NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE_OLD, ref on, sizeof(int));
            }
            catch { /* older Windows: titlebar stays default, still fully functional */ }
        }

        // ---- Theme -------------------------------------------------------------
        private void OnSetTheme(string mode)
        {
            _cfg.Theme = mode;
            _cfg.Save();
            Theme.SetMode(mode);
            LoadThemedContent();   // re-skin with the new palette
            ApplyDarkTitlebar();   // window has a handle by now, so this takes effect immediately
            Status("Theme set to " + mode + ".");
        }

        private void UpdateThemeButtons()
        {
            SetSegmentState(_themeAutoButton, Theme.Mode == AppTheme.Auto);
            SetSegmentState(_themeLightButton, Theme.Mode == AppTheme.Light);
            SetSegmentState(_themeDarkButton, Theme.Mode == AppTheme.Dark);
        }

        private static void SetSegmentState(Button b, bool active)
        {
            if (b == null) return;
            b.Background = ThemeBrush(active ? "Accent" : "BtnBg");
            b.Foreground = ThemeBrush(active ? "OnAccent" : "TextSecondary");
            b.FontWeight = active ? FontWeights.SemiBold : FontWeights.Normal;
        }

        private static System.Windows.Media.Brush ThemeBrush(string token)
        {
            try { return (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString(Theme.Get(token)); }
            catch { return System.Windows.Media.Brushes.Gray; }
        }

        // ---- Add a project -----------------------------------------------------
        private void OnBrowse()
        {
            try
            {
                using (var dlg = new System.Windows.Forms.FolderBrowserDialog())
                {
                    dlg.Description = "Select the project folder (you can also paste a \\\\server\\share path)";
                    dlg.ShowNewFolderButton = false;
                    if (!string.IsNullOrWhiteSpace(_pathBox.Text)) dlg.SelectedPath = _pathBox.Text.Trim();
                    if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                        _pathBox.Text = dlg.SelectedPath;
                }
            }
            catch (Exception ex) { Status("Couldn't open the folder picker: " + ex.Message); }
        }

        private void UpdatePreview()
        {
            string number = ProjectService.DeriveProjectNumber(_pathBox.Text?.Trim());
            string display = ProjectService.BuildDisplayName(_nameBox.Text, number, _cfg.Separator);
            _previewText.Text = string.IsNullOrEmpty(display) ? "—" : display;
        }

        private void OnCreate()
        {
            try
            {
                var link = ProjectService.CreateProject(_nameBox.Text, _pathBox.Text, _cfg);
                _nameBox.Text = "";
                _pathBox.Text = "";
                RefreshList();
                Status(link.Pinned
                    ? "Added \"" + link.DisplayName + "\" (folder pinned)."
                    : "Added \"" + link.DisplayName + "\" — click 'Pin Projects folder' to pin it.");
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(this, ex.Message, AppPaths.AppName,
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // ---- Project list actions ----------------------------------------------
        private ProjectLink Selected => _projectsList.SelectedItem as ProjectLink;

        private void OnOpen()
        {
            var p = Selected; if (p == null) return;
            try { Process.Start(new ProcessStartInfo(p.LinkPath) { UseShellExecute = true }); }
            catch (Exception ex) { Status("Couldn't open: " + ex.Message); }
        }

        private void OnRepin()
        {
            bool ok = ProjectsHubService.PinHub();
            Status(ok
                ? "Pinned your " + ProjectsHubService.HubFolderName + " folder to Quick Access."
                : "Couldn't pin — open the folder and right-click > Pin to Quick Access.");
            RefreshList();
        }

        private void OnRemove()
        {
            var p = Selected; if (p == null) return;
            var answer = System.Windows.MessageBox.Show(this,
                "Remove the shortcut \"" + p.DisplayName + "\"?\n\n" +
                "This deletes only the local shortcut. Your real folder on the network is NOT touched.",
                AppPaths.AppName, MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (answer != MessageBoxResult.Yes) return;
            try
            {
                ProjectService.RemoveProject(p);
                RefreshList();
                Status("Removed \"" + p.DisplayName + "\". The network folder is untouched.");
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(this, ex.Message, AppPaths.AppName,
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RefreshList()
        {
            var items = ProjectService.ListProjects();
            _projectsList.ItemsSource = items;
            _emptyHint.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            UpdateSelectionButtons();
            Status(items.Count == 0 ? "No projects yet." : items.Count + " project(s).");
        }

        private void UpdateSelectionButtons()
        {
            bool has = Selected != null;
            _openButton.IsEnabled = has;
            _removeButton.IsEnabled = has;
            // "Pin Projects folder" acts on the hub, not a selection - always enabled.
        }

        // ---- Settings (collapsible) --------------------------------------------
        private void OnToggleSettings()
        {
            _settingsOpen = !_settingsOpen;
            _settingsPanel.Visibility = _settingsOpen ? Visibility.Visible : Visibility.Collapsed;
            _settingsToggle.Content = (_settingsOpen ? "▾  " : "▸  ") + "Settings";
        }

        private void RefreshStorageInfo()
        {
            _hubNameBox.Text = ProjectsHubService.HubFolderName;
            _storagePathText.Text = ProjectsHubService.HubDir;
            _storagePathText.ToolTip = ProjectsHubService.HubDir; // full path on hover (text is trimmed)
        }

        private void OnRename()
        {
            try
            {
                ProjectsHubService.RenameHub(_hubNameBox.Text, _cfg);
                RefreshStorageInfo();
                RefreshList();
                Status("Quick Access folder is now named \"" + ProjectsHubService.HubFolderName + "\".");
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(this, ex.Message, AppPaths.AppName,
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void OnOpenStorage()
        {
            try
            {
                ProjectsHubService.EnsureHub();
                Process.Start(new ProcessStartInfo(ProjectsHubService.HubDir) { UseShellExecute = true });
            }
            catch (Exception ex) { Status("Couldn't open: " + ex.Message); }
        }

        // ---- Readiness / right-click menu / self-test --------------------------
        private void UpdateReadiness()
        {
            _createButton.IsEnabled = true; // hub/.lnk model needs no admin or privilege
            UpdateShellMenuButton();
        }

        private void UpdateShellMenuButton()
        {
            try
            {
                bool on = RightClickMenu.IsEnabled();
                _shellMenuButton.Content = on ? "Right-click menu: On" : "Right-click menu: Off";
            }
            catch { _shellMenuButton.Content = "Right-click menu"; }
        }

        private void OnToggleShellMenu()
        {
            try
            {
                string status = RightClickMenu.IsEnabled()
                    ? RightClickMenu.Disable()
                    : RightClickMenu.Enable();
                Status(status);
            }
            catch (Exception ex) { Status("Couldn't change the right-click menu: " + ex.Message); }
            UpdateShellMenuButton();
        }

        private void OnSelfTest()
        {
            var report = SelfTest.Run();
            System.Windows.MessageBox.Show(this,
                report.Summary + "\n\n" + report.Details,
                AppPaths.AppName + " — Self-test",
                MessageBoxButton.OK,
                report.Passed ? MessageBoxImage.Information : MessageBoxImage.Warning);
        }

        // ---- Uninstall --------------------------------------------------------
        private void OnUninstall()
        {
            var confirm = System.Windows.MessageBox.Show(this,
                "Uninstall Project Pinner from this PC?\n\n" +
                "This removes the right-click menu, the app itself, the Start Menu shortcut, " +
                "the Quick Access pin, and all of your alias shortcuts.\n\n" +
                "Your real folders on the network are NOT touched — the aliases are only " +
                "local shortcuts.",
                AppPaths.AppName, MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes) return;

            try
            {
                Installer.FullUninstall();
                System.Windows.MessageBox.Show(this,
                    "Project Pinner has been removed. This window will now close.",
                    AppPaths.AppName, MessageBoxButton.OK, MessageBoxImage.Information);
                Installer.ScheduleInstallDirDeletion(); // deletes the install dir after we exit
                Close();
            }
            catch (Exception ex) { Status("Uninstall error: " + ex.Message); }
        }

        private void Status(string msg) => _statusText.Text = msg;

        // ---- UI (runtime-parsed XAML; @@Token@@ placeholders are replaced with the active
        //         theme's palette by Theme.Apply before parsing; single quotes avoid C# escaping) --
        private const string Xaml = @"
<Grid xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
      xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'
      TextElement.Foreground='@@TextPrimary@@' TextElement.FontFamily='Segoe UI'
      TextOptions.TextFormattingMode='Display' Margin='0'>
  <Grid.Resources>
    <Style TargetType='TextBlock'>
      <Setter Property='Foreground' Value='@@TextPrimary@@'/>
    </Style>

    <Style x:Key='Label' TargetType='TextBlock'>
      <Setter Property='Foreground' Value='@@TextMuted@@'/>
      <Setter Property='FontSize' Value='11'/>
    </Style>

    <Style x:Key='Input' TargetType='TextBox'>
      <Setter Property='Foreground' Value='@@TextPrimary@@'/>
      <Setter Property='CaretBrush' Value='@@TextPrimary@@'/>
      <Setter Property='Background' Value='@@InputBg@@'/>
      <Setter Property='BorderBrush' Value='@@InputBorder@@'/>
      <Setter Property='BorderThickness' Value='1'/>
      <Setter Property='FontSize' Value='13'/>
      <Setter Property='Padding' Value='9,5'/>
      <Setter Property='VerticalContentAlignment' Value='Center'/>
      <Setter Property='Template'>
        <Setter.Value>
          <ControlTemplate TargetType='TextBox'>
            <Border Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}'
                    BorderThickness='{TemplateBinding BorderThickness}' CornerRadius='6'>
              <ScrollViewer x:Name='PART_ContentHost' Margin='{TemplateBinding Padding}'/>
            </Border>
            <ControlTemplate.Triggers>
              <Trigger Property='IsFocused' Value='True'>
                <Setter Property='BorderBrush' Value='@@Accent@@'/>
              </Trigger>
            </ControlTemplate.Triggers>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
    </Style>

    <Style x:Key='BtnBase' TargetType='Button'>
      <Setter Property='Foreground' Value='@@TextPrimary@@'/>
      <Setter Property='FontSize' Value='13'/>
      <Setter Property='Padding' Value='10,6'/>
      <Setter Property='Cursor' Value='Hand'/>
      <Setter Property='SnapsToDevicePixels' Value='True'/>
      <Setter Property='Background' Value='@@BtnBg@@'/>
      <Setter Property='Template'>
        <Setter.Value>
          <ControlTemplate TargetType='Button'>
            <Border x:Name='b' Background='{TemplateBinding Background}' CornerRadius='6'
                    Padding='{TemplateBinding Padding}'>
              <ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/>
            </Border>
            <ControlTemplate.Triggers>
              <Trigger Property='IsMouseOver' Value='True'>
                <Setter TargetName='b' Property='Opacity' Value='0.88'/>
              </Trigger>
              <Trigger Property='IsEnabled' Value='False'>
                <Setter TargetName='b' Property='Background' Value='@@BtnDisabledBg@@'/>
                <Setter Property='Foreground' Value='@@TextDisabled@@'/>
                <Setter Property='Cursor' Value='Arrow'/>
              </Trigger>
            </ControlTemplate.Triggers>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
    </Style>

    <Style x:Key='PrimaryButton' TargetType='Button' BasedOn='{StaticResource BtnBase}'>
      <Setter Property='Background' Value='@@Accent@@'/>
      <Setter Property='Foreground' Value='@@OnAccent@@'/>
      <Setter Property='FontWeight' Value='SemiBold'/>
    </Style>

    <Style x:Key='GhostButton' TargetType='Button' BasedOn='{StaticResource BtnBase}'>
      <Setter Property='Background' Value='@@BtnBg@@'/>
    </Style>

    <Style x:Key='SegButton' TargetType='Button' BasedOn='{StaticResource BtnBase}'>
      <Setter Property='Background' Value='@@BtnBg@@'/>
      <Setter Property='Foreground' Value='@@TextSecondary@@'/>
      <Setter Property='FontSize' Value='12'/>
      <Setter Property='Padding' Value='14,4'/>
    </Style>

    <Style x:Key='DangerButton' TargetType='Button' BasedOn='{StaticResource BtnBase}'>
      <Setter Property='Background' Value='@@DangerBg@@'/>
      <Setter Property='Foreground' Value='@@DangerFg@@'/>
    </Style>

    <Style x:Key='LinkButton' TargetType='Button' BasedOn='{StaticResource BtnBase}'>
      <Setter Property='Background' Value='Transparent'/>
      <Setter Property='Foreground' Value='@@TextSecondary@@'/>
      <Setter Property='Padding' Value='4,2'/>
      <Setter Property='FontSize' Value='11.5'/>
    </Style>

    <Style TargetType='CheckBox'>
      <Setter Property='Foreground' Value='@@TextStrong@@'/>
      <Setter Property='FontSize' Value='12'/>
    </Style>

    <DataTemplate x:Key='ProjectItem'>
      <StackPanel Margin='2,4'>
        <!-- Foreground is set explicitly: inside an item data template the implicit
             TextBlock style above isn't applied, so without this the title falls back
             to the system default (black) and renders dark-on-dark on the card. -->
        <TextBlock Text='{Binding DisplayName}' FontSize='13' FontWeight='SemiBold' Foreground='@@TextPrimary@@'/>
        <TextBlock Text='{Binding Target}' FontSize='10.5' Foreground='@@TextSecondary@@' TextTrimming='CharacterEllipsis'/>
      </StackPanel>
    </DataTemplate>

    <Style x:Key='ProjectList' TargetType='ListBox'>
      <Setter Property='Background' Value='Transparent'/>
      <Setter Property='BorderThickness' Value='0'/>
      <Setter Property='ItemTemplate' Value='{StaticResource ProjectItem}'/>
      <Setter Property='ScrollViewer.HorizontalScrollBarVisibility' Value='Disabled'/>
      <Setter Property='ItemContainerStyle'>
        <Setter.Value>
          <Style TargetType='ListBoxItem'>
            <Setter Property='Padding' Value='9,1'/>
            <Setter Property='Margin' Value='0,1'/>
            <Setter Property='Template'>
              <Setter.Value>
                <ControlTemplate TargetType='ListBoxItem'>
                  <Border x:Name='bd' Background='Transparent' CornerRadius='6' Padding='{TemplateBinding Padding}'>
                    <ContentPresenter/>
                  </Border>
                  <ControlTemplate.Triggers>
                    <Trigger Property='IsMouseOver' Value='True'>
                      <Setter TargetName='bd' Property='Background' Value='@@ItemHover@@'/>
                    </Trigger>
                    <Trigger Property='IsSelected' Value='True'>
                      <Setter TargetName='bd' Property='Background' Value='@@ItemSelected@@'/>
                    </Trigger>
                  </ControlTemplate.Triggers>
                </ControlTemplate>
              </Setter.Value>
            </Setter>
          </Style>
        </Setter.Value>
      </Setter>
    </Style>
  </Grid.Resources>

  <Grid.RowDefinitions>
    <RowDefinition Height='Auto'/>
    <RowDefinition Height='Auto'/>
    <RowDefinition Height='*'/>
    <RowDefinition Height='Auto'/>
    <RowDefinition Height='Auto'/>
  </Grid.RowDefinitions>

  <!-- Header -->
  <Grid Grid.Row='0' Margin='14,12,14,4'>
    <Grid.ColumnDefinitions><ColumnDefinition Width='*'/><ColumnDefinition Width='Auto'/></Grid.ColumnDefinitions>
    <StackPanel Grid.Column='0'>
      <TextBlock Text='Project Pinner' FontSize='15' FontWeight='Bold'/>
      <TextBlock Text='Alias shortcuts in one folder, pinned to Quick Access.'
                 FontSize='11' Foreground='@@TextSecondary@@' Margin='0,1,0,0'/>
    </StackPanel>
    <Button x:Name='SettingsToggle' Grid.Column='1' Content='&#9656;  Settings'
            Style='{StaticResource LinkButton}' VerticalAlignment='Top'
            ToolTip='Theme, the Quick Access folder name and location, and the safety self-test'/>
  </Grid>

  <!-- Settings (collapsible) -->
  <Border x:Name='SettingsPanel' Grid.Row='1' Margin='14,2,14,4' CornerRadius='8'
          Background='@@PanelBg@@' BorderBrush='@@Border@@' BorderThickness='1' Padding='12,10' Visibility='Collapsed'>
    <StackPanel>
      <Grid>
        <Grid.ColumnDefinitions><ColumnDefinition Width='*'/><ColumnDefinition Width='*'/></Grid.ColumnDefinitions>
        <StackPanel Grid.Column='0' Margin='0,0,8,0'>
          <TextBlock Text='QUICK ACCESS FOLDER NAME' Style='{StaticResource Label}'/>
          <Grid Margin='0,4,0,0'>
            <Grid.ColumnDefinitions><ColumnDefinition Width='*'/><ColumnDefinition Width='Auto'/></Grid.ColumnDefinitions>
            <TextBox x:Name='HubNameBox' Grid.Column='0' Style='{StaticResource Input}'
                     ToolTip='The name this folder shows under Quick Access (e.g. Projects, Active Jobs)'/>
            <Button x:Name='RenameButton' Grid.Column='1' Content='Rename' Style='{StaticResource GhostButton}' Margin='6,0,0,0'
                    ToolTip='Rename the local folder and re-pin it. Your network folders are not affected.'/>
          </Grid>
        </StackPanel>
        <StackPanel Grid.Column='1' Margin='8,0,0,0'>
          <TextBlock Text='STORED LOCALLY (only shortcuts)' Style='{StaticResource Label}' TextTrimming='CharacterEllipsis'
                     ToolTip='Just shortcuts live here — your network folders are never moved or renamed.'/>
          <Grid Margin='0,4,0,0'>
            <Grid.ColumnDefinitions><ColumnDefinition Width='*'/><ColumnDefinition Width='Auto'/></Grid.ColumnDefinitions>
            <TextBlock x:Name='StoragePathText' Grid.Column='0' FontSize='11' Foreground='@@TextStrong@@'
                       VerticalAlignment='Center' TextTrimming='CharacterEllipsis'/>
            <Button x:Name='OpenStorageButton' Grid.Column='1' Content='Open' Style='{StaticResource GhostButton}' Margin='6,0,0,0'
                    ToolTip='Open the local folder that holds your shortcuts'/>
          </Grid>
        </StackPanel>
      </Grid>

      <Border Height='1' Background='@@Border@@' Margin='0,12,0,10'/>

      <Grid>
        <Grid.ColumnDefinitions><ColumnDefinition Width='*'/><ColumnDefinition Width='Auto'/></Grid.ColumnDefinitions>
        <StackPanel Grid.Column='0'>
          <TextBlock Text='APPEARANCE' Style='{StaticResource Label}'/>
          <StackPanel Orientation='Horizontal' Margin='0,5,0,0'>
            <Button x:Name='ThemeAutoButton' Content='Auto' Style='{StaticResource SegButton}' Margin='0,0,6,0'
                    ToolTip='Match the Windows light/dark setting'/>
            <Button x:Name='ThemeLightButton' Content='Light' Style='{StaticResource SegButton}' Margin='0,0,6,0'
                    ToolTip='Always use the light theme'/>
            <Button x:Name='ThemeDarkButton' Content='Dark' Style='{StaticResource SegButton}'
                    ToolTip='Always use the dark theme'/>
          </StackPanel>
        </StackPanel>
        <StackPanel Grid.Column='1' VerticalAlignment='Bottom'>
          <Button x:Name='SelfTestButton' Content='Run safety self-test' Style='{StaticResource LinkButton}'
                  HorizontalAlignment='Right'
                  ToolTip='Safe self-test: proves removing a shortcut never touches the real folder'/>
        </StackPanel>
      </Grid>
    </StackPanel>
  </Border>

  <!-- Body: two columns (add form | project list) -->
  <Grid Grid.Row='2' Margin='14,4,14,4'>
    <Grid.ColumnDefinitions>
      <ColumnDefinition Width='300'/>
      <ColumnDefinition Width='*'/>
    </Grid.ColumnDefinitions>

    <Border Grid.Column='0' Margin='0,0,6,0' CornerRadius='10' Background='@@CardBg@@'
            BorderBrush='@@Border@@' BorderThickness='1' Padding='12'>
      <StackPanel>
        <TextBlock Text='Add a project' FontSize='13.5' FontWeight='SemiBold' Margin='0,0,0,8'/>
        <TextBlock Text='PROJECT FOLDER (network path)' Style='{StaticResource Label}' Margin='1,0,0,4'/>
        <Grid>
          <Grid.ColumnDefinitions><ColumnDefinition Width='*'/><ColumnDefinition Width='Auto'/></Grid.ColumnDefinitions>
          <TextBox x:Name='PathBox' Grid.Column='0' Style='{StaticResource Input}'
                   ToolTip='Paste a network path like \\server\share\1003948572, or click Browse'/>
          <Button x:Name='BrowseButton' Grid.Column='1' Content='Browse' Style='{StaticResource GhostButton}' Margin='6,0,0,0'
                  ToolTip='Browse for the project folder'/>
        </Grid>
        <TextBlock Text='ALIAS' Style='{StaticResource Label}' Margin='1,7,0,3'/>
        <TextBox x:Name='NameBox' Style='{StaticResource Input}'
                 ToolTip='A short, readable alias for this folder (goes in front of the project number)'/>
        <TextBlock Text='WILL APPEAR AS' Style='{StaticResource Label}' Margin='1,7,0,3'/>
        <TextBlock x:Name='PreviewText' FontSize='13' Foreground='@@AccentText@@' FontWeight='SemiBold' TextTrimming='CharacterEllipsis'
                   ToolTip='Preview of the shortcut name as it will appear in your Projects folder'/>
        <CheckBox x:Name='PinCheck' Content='Pin folder to Quick Access' Margin='1,7,0,0'
                  ToolTip='Also pin the Projects folder to Quick Access (needed only once)'/>
        <Button x:Name='CreateButton' Content='Create &amp; Pin' Style='{StaticResource PrimaryButton}'
                HorizontalAlignment='Stretch' Margin='0,10,0,0'
                ToolTip='Create the shortcut for this project and pin the Projects folder'/>
      </StackPanel>
    </Border>

    <Border Grid.Column='1' Margin='6,0,0,0' CornerRadius='10' Background='@@CardBg@@'
            BorderBrush='@@Border@@' BorderThickness='1' Padding='11,10'>
      <Grid>
        <Grid.RowDefinitions>
          <RowDefinition Height='Auto'/>
          <RowDefinition Height='*'/>
          <RowDefinition Height='Auto'/>
        </Grid.RowDefinitions>
        <Grid Grid.Row='0' Margin='3,0,3,5'>
          <Grid.ColumnDefinitions><ColumnDefinition Width='*'/><ColumnDefinition Width='Auto'/></Grid.ColumnDefinitions>
          <TextBlock Grid.Column='0' Text='Your projects' FontSize='13.5' FontWeight='SemiBold' VerticalAlignment='Center'/>
          <Button x:Name='RefreshButton' Grid.Column='1' Content='Refresh' Style='{StaticResource LinkButton}'
                  ToolTip='Reload the project list from disk'/>
        </Grid>
        <ListBox x:Name='ProjectsList' Grid.Row='1' Style='{StaticResource ProjectList}'
                 ToolTip='Your pinned projects. Select one, then Open or Remove.'/>
        <TextBlock x:Name='EmptyHint' Grid.Row='1' Visibility='Collapsed'
                   Text='No projects yet.&#10;Add one on the left, or right-click a folder in File Explorer.'
                   Foreground='@@TextSecondary@@' FontSize='12' TextAlignment='Center' TextWrapping='Wrap'
                   HorizontalAlignment='Center' VerticalAlignment='Center'/>
        <StackPanel Grid.Row='2' Orientation='Horizontal' Margin='2,7,0,0'>
          <Button x:Name='OpenButton' Content='Open' Style='{StaticResource GhostButton}' Margin='0,0,6,0'
                  ToolTip='Open the selected project in File Explorer'/>
          <Button x:Name='RepinButton' Content='Pin Projects folder' Style='{StaticResource GhostButton}' Margin='0,0,6,0'
                  ToolTip='Pin the Projects folder to Quick Access (a one-time setup step, if it is not already pinned)'/>
          <Button x:Name='RemoveButton' Content='Remove' Style='{StaticResource DangerButton}'
                  ToolTip='Delete only this shortcut. Your network folder is NOT touched.'/>
        </StackPanel>
      </Grid>
    </Border>
  </Grid>

  <!-- Footer -->
  <Grid Grid.Row='3' Margin='14,2,14,2'>
    <Grid.ColumnDefinitions><ColumnDefinition Width='*'/><ColumnDefinition Width='Auto'/></Grid.ColumnDefinitions>
    <TextBlock x:Name='StatusText' Grid.Column='0' Foreground='@@TextSecondary@@' FontSize='11.5'
               VerticalAlignment='Center' TextTrimming='CharacterEllipsis'/>
    <StackPanel Grid.Column='1' Orientation='Horizontal'>
      <Button x:Name='ShellMenuButton' Content='Right-click menu' Style='{StaticResource LinkButton}' Margin='0,0,14,0'
              ToolTip='Turn the folder right-click Pin with alias menu on or off'/>
      <Button x:Name='UninstallButton' Content='Uninstall' Style='{StaticResource LinkButton}' Foreground='@@DangerLink@@'
              ToolTip='Remove the right-click menu, Start Menu shortcut, and Quick Access pin from this PC'/>
    </StackPanel>
  </Grid>

  <!-- Attribution -->
  <TextBlock Grid.Row='4' Text='Developed by Waldo Development LLC' HorizontalAlignment='Center'
             Foreground='@@TextFaint@@' FontSize='10' Margin='0,2,0,8'/>
</Grid>";
    }
}

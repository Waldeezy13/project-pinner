using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Markup;

namespace ProjectPinner
{
    internal sealed class MainWindow : Window
    {
        private readonly Config _cfg;

        private TextBox _pathBox, _nameBox, _hubNameBox;
        private TextBlock _previewText, _statusText, _storagePathText;
        private Button _browseButton, _createButton, _openButton, _repinButton, _removeButton,
                       _refreshButton, _selfTestButton, _shellMenuButton,
                       _renameButton, _openStorageButton, _settingsToggle, _uninstallButton;
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
            Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#15171C");
            Icon = AppIcon.Get();

            var content = (FrameworkElement)XamlReader.Parse(Xaml);
            Content = content;

            BindControls(content);
            WireEvents();
            UpdatePreview();
            RefreshStorageInfo();

            int cleared = 0;
            try { cleared = ProjectService.CleanupLegacyPins(); } catch { }

            RefreshList();
            UpdateReadiness();

            if (cleared > 0)
                Status("Cleaned up " + cleared + " old wrongly-named pin(s) from a previous version.");
        }

        private T Find<T>(FrameworkElement root, string name) where T : FrameworkElement
            => (T)root.FindName(name);

        private void BindControls(FrameworkElement root)
        {
            _pathBox = Find<TextBox>(root, "PathBox");
            _nameBox = Find<TextBox>(root, "NameBox");
            _hubNameBox = Find<TextBox>(root, "HubNameBox");
            _previewText = Find<TextBlock>(root, "PreviewText");
            _statusText = Find<TextBlock>(root, "StatusText");
            _storagePathText = Find<TextBlock>(root, "StoragePathText");
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
            _pinCheck.Checked += (s, e) => { _cfg.AutoPin = true; _cfg.Save(); };
            _pinCheck.Unchecked += (s, e) => { _cfg.AutoPin = false; _cfg.Save(); };
        }

        // ---- Dark titlebar -----------------------------------------------------
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            try
            {
                var hwnd = new WindowInteropHelper(this).Handle;
                int on = 1;
                if (NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE, ref on, sizeof(int)) != 0)
                    NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE_OLD, ref on, sizeof(int));
            }
            catch { /* older Windows: light titlebar, still fully functional */ }
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
                _cfg.AutoPin = _pinCheck.IsChecked == true;
                var link = ProjectService.CreateProject(_nameBox.Text, _pathBox.Text, _cfg);
                _nameBox.Text = "";
                _pathBox.Text = "";
                RefreshList();
                Status(link.Pinned
                    ? "Added \"" + link.DisplayName + "\" (folder pinned)."
                    : "Added \"" + link.DisplayName + "\" — click 'Pin folder' to pin it.");
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
            UpdateSelectionButtons();
            Status(items.Count == 0 ? "No projects yet." : items.Count + " project(s).");
        }

        private void UpdateSelectionButtons()
        {
            bool has = Selected != null;
            _openButton.IsEnabled = has;
            _removeButton.IsEnabled = has;
            // "Pin folder" acts on the hub, not a selection - always enabled.
        }

        // ---- Settings (collapsible) --------------------------------------------
        private void OnToggleSettings()
        {
            _settingsOpen = !_settingsOpen;
            _settingsPanel.Visibility = _settingsOpen ? Visibility.Visible : Visibility.Collapsed;
            _settingsToggle.Content = (_settingsOpen ? "▾  " : "▸  ") + "Folder name & location";
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
                bool on = ShellMenuService.IsRegistered();
                _shellMenuButton.Content = on ? "Right-click menu: On" : "Right-click menu: Off";
            }
            catch { _shellMenuButton.Content = "Right-click menu"; }
        }

        private void OnToggleShellMenu()
        {
            try
            {
                if (ShellMenuService.IsRegistered())
                {
                    ShellMenuService.Unregister();
                    Status("Removed the right-click \"Pin with alias\" menu entry.");
                }
                else
                {
                    Installer.InstallFilesForCurrentUser(); // ensures the installed exe exists, then registers
                    ShellMenuService.Register();
                    Status("Added \"Pin with alias to Quick Access\" to the folder right-click menu.");
                }
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
            string dataPath = AppPaths.InstallRoot;
            var confirm = System.Windows.MessageBox.Show(this,
                "Uninstall Project Pinner from this PC?\n\n" +
                "This will remove:\n" +
                "  • The right-click “Pin with alias” menu\n" +
                "  • The Start Menu shortcut\n" +
                "  • The Quick Access pin\n\n" +
                "Your local shortcuts folder will NOT be deleted. " +
                "Delete it manually when ready:\n" + dataPath,
                AppPaths.AppName, MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes) return;

            try
            {
                Installer.Uninstall();
                System.Windows.MessageBox.Show(this,
                    "Project Pinner has been uninstalled.\n\n" +
                    "Your shortcuts folder remains at:\n" + dataPath + "\n\n" +
                    "Delete it manually when you’re ready.",
                    AppPaths.AppName, MessageBoxButton.OK, MessageBoxImage.Information);
                Close();
            }
            catch (Exception ex) { Status("Uninstall error: " + ex.Message); }
        }

        private void Status(string msg) => _statusText.Text = msg;

        // ---- UI (runtime-parsed XAML; single quotes avoid C# escaping) ---------
        private const string Xaml = @"
<Grid xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
      xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'
      TextElement.Foreground='#E6E8EC' TextElement.FontFamily='Segoe UI'
      TextOptions.TextFormattingMode='Display' Margin='0'>
  <Grid.Resources>
    <Style TargetType='TextBlock'>
      <Setter Property='Foreground' Value='#E6E8EC'/>
    </Style>

    <Style x:Key='Label' TargetType='TextBlock'>
      <Setter Property='Foreground' Value='#8A909A'/>
      <Setter Property='FontSize' Value='10'/>
    </Style>

    <Style x:Key='Input' TargetType='TextBox'>
      <Setter Property='Foreground' Value='#E6E8EC'/>
      <Setter Property='CaretBrush' Value='#E6E8EC'/>
      <Setter Property='Background' Value='#22262E'/>
      <Setter Property='BorderBrush' Value='#3A3F4A'/>
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
                <Setter Property='BorderBrush' Value='#4F8CFF'/>
              </Trigger>
            </ControlTemplate.Triggers>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
    </Style>

    <Style x:Key='BtnBase' TargetType='Button'>
      <Setter Property='Foreground' Value='#E6E8EC'/>
      <Setter Property='FontSize' Value='13'/>
      <Setter Property='Padding' Value='10,6'/>
      <Setter Property='Cursor' Value='Hand'/>
      <Setter Property='SnapsToDevicePixels' Value='True'/>
      <Setter Property='Background' Value='#2B303A'/>
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
                <Setter TargetName='b' Property='Background' Value='#262A32'/>
                <Setter Property='Foreground' Value='#6B7079'/>
                <Setter Property='Cursor' Value='Arrow'/>
              </Trigger>
            </ControlTemplate.Triggers>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
    </Style>

    <Style x:Key='PrimaryButton' TargetType='Button' BasedOn='{StaticResource BtnBase}'>
      <Setter Property='Background' Value='#4F8CFF'/>
      <Setter Property='Foreground' Value='White'/>
      <Setter Property='FontWeight' Value='SemiBold'/>
    </Style>

    <Style x:Key='GhostButton' TargetType='Button' BasedOn='{StaticResource BtnBase}'>
      <Setter Property='Background' Value='#2B303A'/>
    </Style>

    <Style x:Key='DangerButton' TargetType='Button' BasedOn='{StaticResource BtnBase}'>
      <Setter Property='Background' Value='#3A2A2E'/>
      <Setter Property='Foreground' Value='#FF8A82'/>
    </Style>

    <Style x:Key='LinkButton' TargetType='Button' BasedOn='{StaticResource BtnBase}'>
      <Setter Property='Background' Value='Transparent'/>
      <Setter Property='Foreground' Value='#9AA0AA'/>
      <Setter Property='Padding' Value='4,2'/>
      <Setter Property='FontSize' Value='11.5'/>
    </Style>

    <Style TargetType='CheckBox'>
      <Setter Property='Foreground' Value='#C7CCD4'/>
      <Setter Property='FontSize' Value='12'/>
    </Style>

    <DataTemplate x:Key='ProjectItem'>
      <StackPanel Margin='2,4'>
        <TextBlock Text='{Binding DisplayName}' FontSize='13' FontWeight='SemiBold'/>
        <TextBlock Text='{Binding Target}' FontSize='10.5' Foreground='#9AA0AA' TextTrimming='CharacterEllipsis'/>
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
                      <Setter TargetName='bd' Property='Background' Value='#20252E'/>
                    </Trigger>
                    <Trigger Property='IsSelected' Value='True'>
                      <Setter TargetName='bd' Property='Background' Value='#26344A'/>
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
      <TextBlock Text='Friendly-named shortcuts in one folder pinned to Quick Access.'
                 FontSize='11' Foreground='#9AA0AA' Margin='0,1,0,0'/>
    </StackPanel>
    <Button x:Name='SettingsToggle' Grid.Column='1' Content='▸  Folder name &amp; location'
            Style='{StaticResource LinkButton}' VerticalAlignment='Top'
            ToolTip='Rename the Quick Access folder, or see and open where shortcuts are stored locally'/>
  </Grid>

  <!-- Settings (collapsible) -->
  <Border x:Name='SettingsPanel' Grid.Row='1' Margin='14,2,14,4' CornerRadius='8'
          Background='#171A20' BorderBrush='#2B303A' BorderThickness='1' Padding='12,10' Visibility='Collapsed'>
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
          <TextBlock x:Name='StoragePathText' Grid.Column='0' FontSize='11' Foreground='#C7CCD4'
                     VerticalAlignment='Center' TextTrimming='CharacterEllipsis'/>
          <Button x:Name='OpenStorageButton' Grid.Column='1' Content='Open' Style='{StaticResource GhostButton}' Margin='6,0,0,0'
                  ToolTip='Open the local folder that holds your shortcuts'/>
        </Grid>
      </StackPanel>
    </Grid>
  </Border>

  <!-- Body: two columns (add form | project list) -->
  <Grid Grid.Row='2' Margin='14,4,14,4'>
    <Grid.ColumnDefinitions>
      <ColumnDefinition Width='300'/>
      <ColumnDefinition Width='*'/>
    </Grid.ColumnDefinitions>

    <Border Grid.Column='0' Margin='0,0,6,0' CornerRadius='10' Background='#1B1E25'
            BorderBrush='#2B303A' BorderThickness='1' Padding='12'>
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
        <TextBlock Text='FRIENDLY NAME' Style='{StaticResource Label}' Margin='1,7,0,3'/>
        <TextBox x:Name='NameBox' Style='{StaticResource Input}'
                 ToolTip='A short, readable name for this project (goes in front of the project number)'/>
        <TextBlock Text='WILL APPEAR AS' Style='{StaticResource Label}' Margin='1,7,0,3'/>
        <TextBlock x:Name='PreviewText' FontSize='13' Foreground='#7FB0FF' FontWeight='SemiBold' TextTrimming='CharacterEllipsis'
                   ToolTip='Preview of the shortcut name as it will appear in your Projects folder'/>
        <CheckBox x:Name='PinCheck' Content='Pin folder to Quick Access' Margin='1,7,0,0'
                  ToolTip='Also pin the Projects folder to Quick Access (needed only once)'/>
        <Button x:Name='CreateButton' Content='Create &amp; Pin' Style='{StaticResource PrimaryButton}'
                HorizontalAlignment='Stretch' Margin='0,10,0,0'
                ToolTip='Create the shortcut for this project and pin the Projects folder'/>
      </StackPanel>
    </Border>

    <Border Grid.Column='1' Margin='6,0,0,0' CornerRadius='10' Background='#1B1E25'
            BorderBrush='#2B303A' BorderThickness='1' Padding='11,10'>
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
        <StackPanel Grid.Row='2' Orientation='Horizontal' Margin='2,7,0,0'>
          <Button x:Name='OpenButton' Content='Open' Style='{StaticResource GhostButton}' Margin='0,0,6,0'
                  ToolTip='Open the selected project in File Explorer'/>
          <Button x:Name='RepinButton' Content='Pin folder' Style='{StaticResource GhostButton}' Margin='0,0,6,0'
                  ToolTip='Pin the Projects folder to Quick Access (if it is not already)'/>
          <Button x:Name='RemoveButton' Content='Remove' Style='{StaticResource DangerButton}'
                  ToolTip='Delete only this shortcut. Your network folder is NOT touched.'/>
        </StackPanel>
      </Grid>
    </Border>
  </Grid>

  <!-- Footer -->
  <Grid Grid.Row='3' Margin='14,2,14,2'>
    <Grid.ColumnDefinitions><ColumnDefinition Width='*'/><ColumnDefinition Width='Auto'/></Grid.ColumnDefinitions>
    <TextBlock x:Name='StatusText' Grid.Column='0' Foreground='#9AA0AA' FontSize='11.5'
               VerticalAlignment='Center' TextTrimming='CharacterEllipsis'/>
    <StackPanel Grid.Column='1' Orientation='Horizontal'>
      <Button x:Name='ShellMenuButton' Content='Right-click menu' Style='{StaticResource LinkButton}' Margin='0,0,10,0'
              ToolTip='Turn the folder right-click Pin with alias menu on or off'/>
      <Button x:Name='SelfTestButton' Content='Run self-test' Style='{StaticResource LinkButton}' Margin='0,0,14,0'
              ToolTip='Safe self-test: proves removing a shortcut never touches the real folder'/>
      <Button x:Name='UninstallButton' Content='Uninstall' Style='{StaticResource LinkButton}' Foreground='#C05050'
              ToolTip='Remove the right-click menu, Start Menu shortcut, and Quick Access pin from this PC'/>
    </StackPanel>
  </Grid>

  <!-- Attribution -->
  <TextBlock Grid.Row='4' Text='Developed by Waldo Development LLC' HorizontalAlignment='Center'
             Foreground='#5A6069' FontSize='10' Margin='0,2,0,8'/>
</Grid>";
    }
}

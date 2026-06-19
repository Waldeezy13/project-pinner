using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;

namespace ProjectPinner
{
    /// <summary>
    /// The small dialog launched by the Explorer right-click verb ("--pin &lt;folder&gt;").
    /// The folder is fixed; the user just types an alias and hits Pin.
    /// Auto-sizes to its content (SizeToContent) so it can never clip.
    /// </summary>
    internal sealed class QuickPinWindow : Window
    {
        private readonly Config _cfg;
        private readonly string _path;

        private TextBox _nameBox;
        private TextBlock _previewText;
        private Button _pinButton, _cancelButton;

        public QuickPinWindow(string folderPath, Config cfg)
        {
            _cfg = cfg;
            _path = (folderPath ?? "").Trim().Trim('"');
            ProjectsHubService.HubFolderName =
                string.IsNullOrWhiteSpace(cfg.HubFolderName) ? "Projects" : cfg.HubFolderName;

            Title = "Pin to Quick Access";
            Width = 460;
            SizeToContent = SizeToContent.Height;     // fit content height exactly — no clipping
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#15171C");
            Icon = AppIcon.Get();

            var root = (FrameworkElement)XamlReader.Parse(Xaml.Replace("@@PATH@@", Escape(_path)));
            Content = root;

            _nameBox = (TextBox)root.FindName("NameBox");
            _previewText = (TextBlock)root.FindName("PreviewText");
            _pinButton = (Button)root.FindName("PinButton");
            _cancelButton = (Button)root.FindName("CancelButton");

            _nameBox.TextChanged += (s, e) => UpdatePreview();
            _nameBox.KeyDown += (s, e) => { if (e.Key == Key.Enter) OnPin(); };
            _pinButton.Click += (s, e) => OnPin();
            _cancelButton.Click += (s, e) => Close();

            UpdatePreview();
            Loaded += (s, e) => { _nameBox.Focus(); Keyboard.Focus(_nameBox); };
        }

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
            catch { }
        }

        private void UpdatePreview()
        {
            string number = ProjectService.DeriveProjectNumber(_path);
            string display = ProjectService.BuildDisplayName(_nameBox.Text, number, _cfg.Separator);
            _previewText.Text = string.IsNullOrEmpty(display) ? "—" : display;
        }

        private void OnPin()
        {
            try
            {
                _cfg.AutoPin = true; // this entry point IS "pin", regardless of saved setting
                ProjectService.CreateProject(_nameBox.Text, _path, _cfg);
                Close();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(this, ex.Message, AppPaths.AppName,
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private static string Escape(string s) =>
            (s ?? "").Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")
                     .Replace("'", "&apos;").Replace("\"", "&quot;");

        private const string Xaml = @"
<Grid xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
      xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'
      TextElement.Foreground='#E6E8EC' TextElement.FontFamily='Segoe UI'
      TextOptions.TextFormattingMode='Display' Margin='18,16,18,16'>
  <Grid.Resources>
    <Style TargetType='TextBlock'><Setter Property='Foreground' Value='#E6E8EC'/></Style>
    <Style x:Key='Label' TargetType='TextBlock'>
      <Setter Property='Foreground' Value='#8A909A'/><Setter Property='FontSize' Value='10'/>
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
              <Trigger Property='IsFocused' Value='True'><Setter Property='BorderBrush' Value='#4F8CFF'/></Trigger>
            </ControlTemplate.Triggers>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
    </Style>
    <Style x:Key='Btn' TargetType='Button'>
      <Setter Property='Foreground' Value='#E6E8EC'/>
      <Setter Property='FontSize' Value='13'/>
      <Setter Property='Padding' Value='12,6'/>
      <Setter Property='Cursor' Value='Hand'/>
      <Setter Property='Background' Value='#2B303A'/>
      <Setter Property='Template'>
        <Setter.Value>
          <ControlTemplate TargetType='Button'>
            <Border x:Name='b' Background='{TemplateBinding Background}' CornerRadius='6' Padding='{TemplateBinding Padding}'>
              <ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/>
            </Border>
            <ControlTemplate.Triggers>
              <Trigger Property='IsMouseOver' Value='True'><Setter TargetName='b' Property='Opacity' Value='0.88'/></Trigger>
            </ControlTemplate.Triggers>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
    </Style>
    <Style x:Key='Primary' TargetType='Button' BasedOn='{StaticResource Btn}'>
      <Setter Property='Background' Value='#4F8CFF'/><Setter Property='Foreground' Value='White'/>
      <Setter Property='FontWeight' Value='SemiBold'/>
    </Style>
  </Grid.Resources>

  <StackPanel>
    <TextBlock Text='Pin with an alias' FontSize='15' FontWeight='Bold'/>

    <TextBlock Text='FOLDER' Style='{StaticResource Label}' Margin='1,10,0,3'/>
    <TextBlock Text='@@PATH@@' FontSize='12' Foreground='#C7CCD4' TextTrimming='CharacterEllipsis'/>

    <TextBlock Text='ALIAS' Style='{StaticResource Label}' Margin='1,10,0,4'/>
    <TextBox x:Name='NameBox' Style='{StaticResource Input}'
             ToolTip='A short, readable alias for this folder (goes in front of the project number)'/>

    <TextBlock Text='WILL APPEAR AS' Style='{StaticResource Label}' Margin='1,10,0,2'/>
    <TextBlock x:Name='PreviewText' FontSize='13' Foreground='#7FB0FF' FontWeight='SemiBold'
               TextTrimming='CharacterEllipsis'/>

    <StackPanel Orientation='Horizontal' HorizontalAlignment='Right' Margin='0,18,0,0'>
      <Button x:Name='CancelButton' Content='Cancel' Style='{StaticResource Btn}' Margin='0,0,8,0'
              ToolTip='Close without pinning'/>
      <Button x:Name='PinButton' Content='Pin' Style='{StaticResource Primary}' MinWidth='72'
              ToolTip='Create the shortcut for this folder and pin it to Quick Access'/>
    </StackPanel>

    <TextBlock Text='Developed by Waldo Development LLC' HorizontalAlignment='Center'
               Foreground='#5A6069' FontSize='10' Margin='0,14,0,0'/>
  </StackPanel>
</Grid>";
    }
}

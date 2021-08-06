using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Ryujinx.Ava.Common;
using Ryujinx.Common.Utilities;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Threading.Tasks;

namespace Ryujinx.Ava.Ui.Windows
{
    public class AboutWindow : StyleableWindow
    {
        public AboutWindow()
        {
            Title = $"Ryujinx {Program.Version} - About";

            DataContext = this;

            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
           // Title = $"Ryujinx {Program.Version} - About";

            _ = DownloadPatronsJson();
        }

        public string Version => Program.Version;

        public string Supporters { get; set; }

        public TextBlock SupportersTextBlock { get; set; }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);

            SupportersTextBlock = this.FindControl<TextBlock>("SupportersTextBlock");
        }

        private void Button_OnClick(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                OpenHelper.OpenUrl(button.Tag.ToString());
            }
        }

        private async Task DownloadPatronsJson()
        {
            if (!NetworkInterface.GetIsNetworkAvailable())
            {
                Supporters = "Connection Error.";

                return;
            }

            HttpClient httpClient = new();

            try
            {
                string patreonJsonString = await httpClient.GetStringAsync("https://patreon.ryujinx.org/");

                Supporters = string.Join(", ", JsonHelper.Deserialize<string[]>(patreonJsonString));
            }
            catch
            {
                Supporters = "API Error.";
            }

            await Dispatcher.UIThread.InvokeAsync(() => SupportersTextBlock.Text = Supporters);
        }

        private void AmiiboLabel_OnPointerPressed(object sender, PointerPressedEventArgs e)
        {
            if (sender is TextBlock)
            {
                OpenHelper.OpenUrl("https://amiiboapi.com");
            }
        }
    }
}
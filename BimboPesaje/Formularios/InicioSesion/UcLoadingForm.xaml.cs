using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using UserControl   = System.Windows.Controls.UserControl;
using WpfColor      = System.Windows.Media.Color;
using WpfBrush      = System.Windows.Media.SolidColorBrush;

namespace BimboPesaje.Formularios.InicioSesion
{
    public partial class UcLoadingForm : UserControl
    {
        private readonly UcLoginShell _shell;
        private readonly TaskCompletionSource _tcs = new();

        private static readonly WpfBrush _brandBrush   = new(WpfColor.FromRgb(0x1E, 0x3A, 0x8A));
        private static readonly WpfBrush _successBrush = new(WpfColor.FromRgb(0x10, 0xB9, 0x81));
        private static readonly WpfBrush _primaryBrush = new(WpfColor.FromRgb(0x1A, 0x1F, 0x2E));

        public UcLoadingForm(UcLoginShell shell)
        {
            _shell = shell;
            InitializeComponent();
            Loaded += (_, _) => _ = AnimateStepsAsync();
        }

        public Task CompleteAsync() => _tcs.Task;

        private async Task AnimateStepsAsync()
        {
            var steps = new[]
            {
                (S1Dot, S1Text, 25),
                (S2Dot, S2Text, 55),
                (S3Dot, S3Text, 80),
                (S4Dot, S4Text, 100)
            };

            await Task.Delay(200);

            foreach (var (dot, label, targetPct) in steps)
            {
                Dispatcher.Invoke(() =>
                {
                    dot.BorderBrush  = _brandBrush;
                    label.Foreground = _primaryBrush;
                    label.FontWeight = FontWeights.SemiBold;
                });

                int current = int.Parse(TxtProgress.Text.TrimEnd('%'));
                while (current < targetPct)
                {
                    current++;
                    int pct = current;
                    Dispatcher.Invoke(() => SetProgress(pct));
                    await Task.Delay(18);
                }

                await Task.Delay(180);

                Dispatcher.Invoke(() =>
                {
                    dot.Background   = _successBrush;
                    dot.BorderBrush  = _successBrush;
                    label.Foreground = _successBrush;
                    label.FontWeight = FontWeights.Normal;
                });

                await Task.Delay(120);
            }

            await Task.Delay(300);
            _tcs.TrySetResult();
        }

        private void SetProgress(int pct)
        {
            TxtProgress.Text = $"{pct}%";
            if (ProgressBar.Parent is System.Windows.Controls.Border parent)
                ProgressBar.Width = parent.ActualWidth * pct / 100.0;
        }
    }
}

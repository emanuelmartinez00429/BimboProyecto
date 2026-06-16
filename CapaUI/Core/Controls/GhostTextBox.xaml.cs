using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace CapaUI.Core.Controls
{
    public partial class GhostTextBox : UserControl
    {
        // ── DependencyProperties ─────────────────────────────────────────

        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register(nameof(Text), typeof(string), typeof(GhostTextBox),
                new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnTextPropertyChanged));

        public static readonly DependencyProperty GhostSuffixProperty =
            DependencyProperty.Register(nameof(GhostSuffix), typeof(string), typeof(GhostTextBox),
                new PropertyMetadata("", OnGhostSuffixChanged));

        public static readonly DependencyProperty PlaceholderProperty =
            DependencyProperty.Register(nameof(Placeholder), typeof(string), typeof(GhostTextBox),
                new PropertyMetadata(""));

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public string GhostSuffix
        {
            get => (string)GetValue(GhostSuffixProperty);
            set => SetValue(GhostSuffixProperty, value);
        }

        public string Placeholder
        {
            get => (string)GetValue(PlaceholderProperty);
            set => SetValue(PlaceholderProperty, value);
        }

        // ── Events ───────────────────────────────────────────────────────

        public event EventHandler? SuffixAccepted;
        public event RoutedEventHandler? InnerGotFocus;
        public event RoutedEventHandler? InnerLostFocus;
        public event RoutedEventHandler? TextChanged;

        // ── State ────────────────────────────────────────────────────────

        private bool _updatingText;
        private CancellationTokenSource? _ghostDebounce;

        private static readonly SolidColorBrush _ghostBrush =
            new(Color.FromRgb(0xB0, 0xB8, 0xC4));
        private static readonly SolidColorBrush _placeholderBrush =
            new(Color.FromRgb(0x9C, 0xA3, 0xAF));

        static GhostTextBox()
        {
            _ghostBrush.Freeze();
            _placeholderBrush.Freeze();
        }

        public GhostTextBox()
        {
            InitializeComponent();
        }

        // ── DP callbacks ─────────────────────────────────────────────────

        private static void OnTextPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var ctrl = (GhostTextBox)d;
            if (ctrl._updatingText) return;

            ctrl._updatingText = true;
            ctrl.InnerBox.Text = (string)e.NewValue;
            ctrl._updatingText = false;
            ctrl.ScheduleGhostUpdate();
        }

        private static void OnGhostSuffixChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((GhostTextBox)d).UpdateGhostNow();
        }

        // ── Event handlers ───────────────────────────────────────────────

        private void InnerBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_updatingText) return;

            _updatingText = true;
            Text = InnerBox.Text;
            _updatingText = false;

            ScheduleGhostUpdate();
            TextChanged?.Invoke(this, e);
        }

        private void InnerBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            string input = InnerBox.Text ?? "";
            if (string.IsNullOrEmpty(input) || string.IsNullOrEmpty(GhostSuffix))
                return;

            // Tab or Right arrow at end of text → accept the ghost completion
            if ((e.Key == Key.Tab || e.Key == Key.Right)
                && InnerBox.CaretIndex == input.Length)
            {
                string remaining = GetRemainingSuffix(input);
                if (string.IsNullOrEmpty(remaining)) return;

                _ghostDebounce?.Cancel();

                _updatingText = true;
                InnerBox.Text = input + remaining;
                InnerBox.CaretIndex = InnerBox.Text.Length;
                Text = InnerBox.Text;
                _updatingText = false;

                UpdateGhostNow();
                SuffixAccepted?.Invoke(this, EventArgs.Empty);
                e.Handled = true;
            }
        }

        private void InnerBox_GotFocus(object sender, RoutedEventArgs e)
            => InnerGotFocus?.Invoke(this, e);

        private void InnerBox_LostFocus(object sender, RoutedEventArgs e)
            => InnerLostFocus?.Invoke(this, e);

        // ── Ghost logic ──────────────────────────────────────────────────

        private void ScheduleGhostUpdate()
        {
            _ghostDebounce?.Cancel();

            string input = InnerBox.Text ?? "";

            // Empty → show placeholder immediately (no debounce)
            if (string.IsNullOrEmpty(input))
            {
                GhostDisplay.Text = Placeholder;
                GhostDisplay.Foreground = _placeholderBrush;
                return;
            }

            // Already has the full suffix → hide ghost immediately
            if (IsFullyCompleted(input))
            {
                GhostDisplay.Text = "";
                return;
            }

            // Hide ghost during debounce wait
            GhostDisplay.Text = "";

            _ghostDebounce = new CancellationTokenSource();
            var token = _ghostDebounce.Token;

            _ = DebounceGhostAsync(input, token);
        }

        private async Task DebounceGhostAsync(string input, CancellationToken ct)
        {
            try
            {
                await Task.Delay(500, ct);
                if (ct.IsCancellationRequested) return;

                Dispatcher.Invoke(() =>
                {
                    // Re-check current text (may have changed during delay)
                    string current = InnerBox.Text ?? "";
                    if (current != input) return;
                    ShowGhostFor(current);
                });
            }
            catch (TaskCanceledException) { }
        }

        private void UpdateGhostNow()
        {
            _ghostDebounce?.Cancel();
            string input = InnerBox.Text ?? "";

            if (string.IsNullOrEmpty(input))
            {
                GhostDisplay.Text = Placeholder;
                GhostDisplay.Foreground = _placeholderBrush;
                return;
            }

            if (IsFullyCompleted(input))
            {
                GhostDisplay.Text = "";
                return;
            }

            ShowGhostFor(input);
        }

        private void ShowGhostFor(string input)
        {
            if (string.IsNullOrEmpty(GhostSuffix))
            {
                GhostDisplay.Text = "";
                return;
            }

            string remaining = GetRemainingSuffix(input);
            if (string.IsNullOrEmpty(remaining))
            {
                GhostDisplay.Text = "";
                return;
            }

            // Ghost shows: input (for spacing alignment) + remaining suffix
            GhostDisplay.Text = input + remaining;
            GhostDisplay.Foreground = _ghostBrush;
        }

        /// <summary>
        /// Finds what portion of the suffix the user still needs to type.
        /// Example: suffix="@gmail.com", input="hola@gma" → remaining="il.com"
        /// Example: suffix="@gmail.com", input="hola" → remaining="@gmail.com"
        /// </summary>
        private string GetRemainingSuffix(string input)
        {
            string suffix = GhostSuffix;

            // Find the longest tail of input that matches the beginning of suffix
            // e.g., input="hola@gma", suffix="@gmail.com"
            //        tail "@gma" matches start of "@gmail.com" → remaining = "il.com"
            int maxOverlap = Math.Min(input.Length, suffix.Length);

            for (int len = maxOverlap; len >= 1; len--)
            {
                string inputTail = input.Substring(input.Length - len);
                string suffixHead = suffix.Substring(0, len);

                if (string.Equals(inputTail, suffixHead, StringComparison.OrdinalIgnoreCase))
                    return suffix.Substring(len);
            }

            // No overlap at all → full suffix
            return suffix;
        }

        private bool IsFullyCompleted(string input)
        {
            if (string.IsNullOrEmpty(GhostSuffix)) return true;
            return input.EndsWith(GhostSuffix, StringComparison.OrdinalIgnoreCase);
        }

        // ── Public helpers ───────────────────────────────────────────────

        public string GetFullText()
        {
            string input = InnerBox.Text?.Trim() ?? "";
            if (string.IsNullOrEmpty(input)) return "";

            if (IsFullyCompleted(input)) return input;

            return input + GetRemainingSuffix(input);
        }

        public new bool Focus() => InnerBox.Focus();
    }
}

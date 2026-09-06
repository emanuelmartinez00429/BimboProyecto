using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace CapaUI.Formularios.Dashboard
{
    public partial class DashboardView : UserControl
    {
        private Storyboard? _pulseStoryboard;

        public DashboardView()
        {
            InitializeComponent();
            Loaded += DashboardView_Loaded;
            Unloaded += DashboardView_Unloaded;
        }

        private void DashboardView_Loaded(object sender, RoutedEventArgs e)
        {
            IniciarAnimacionLive();
        }

        private void DashboardView_Unloaded(object sender, RoutedEventArgs e)
        {
            DetenerAnimacionLive();
        }

        private void IniciarAnimacionLive()
        {
            if (_pulseStoryboard != null) return;

            var animOpacity = new DoubleAnimation(1.0, 0.4, TimeSpan.FromSeconds(0.75))
            {
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever
            };
            Storyboard.SetTarget(animOpacity, LivePulseDot);
            Storyboard.SetTargetProperty(animOpacity, new PropertyPath("Opacity"));

            var animScaleX = new DoubleAnimation(1.0, 0.7, TimeSpan.FromSeconds(0.75))
            {
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever
            };
            Storyboard.SetTarget(animScaleX, LivePulseDot);
            Storyboard.SetTargetProperty(animScaleX, new PropertyPath("(UIElement.RenderTransform).(ScaleTransform.ScaleX)"));

            var animScaleY = new DoubleAnimation(1.0, 0.7, TimeSpan.FromSeconds(0.75))
            {
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever
            };
            Storyboard.SetTarget(animScaleY, LivePulseDot);
            Storyboard.SetTargetProperty(animScaleY, new PropertyPath("(UIElement.RenderTransform).(ScaleTransform.ScaleY)"));

            _pulseStoryboard = new Storyboard();
            _pulseStoryboard.Children.Add(animOpacity);
            _pulseStoryboard.Children.Add(animScaleX);
            _pulseStoryboard.Children.Add(animScaleY);
            _pulseStoryboard.Begin();
        }

        private void DetenerAnimacionLive()
        {
            if (_pulseStoryboard is null) return;
            _pulseStoryboard.Stop();
            _pulseStoryboard.Remove();
            _pulseStoryboard.Children.Clear();
            _pulseStoryboard = null;
        }
    }
}

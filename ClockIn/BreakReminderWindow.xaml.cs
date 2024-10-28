using System;
using System.Windows;

namespace ClockIn
{
    public partial class BreakReminderWindow : Window
    {
        public event EventHandler EndBreakClicked;

        public BreakReminderWindow()
        {
            InitializeComponent();
            this.Topmost = true;
            this.ShowInTaskbar = false;
            this.ResizeMode = ResizeMode.NoResize;
            this.WindowStyle = WindowStyle.None;
            this.SizeToContent = SizeToContent.WidthAndHeight;
        }

        private void EndBreakButton_Click(object sender, RoutedEventArgs e)
        {
            EndBreakClicked?.Invoke(this, EventArgs.Empty);
        }
    }
}
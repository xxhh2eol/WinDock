using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace WinDock.Controls
{
    /// <summary>
    /// 单行备注文本。文本超过控件宽度时，鼠标悬停会横向滚动（跑马灯）展示完整内容。
    /// </summary>
    public partial class MarqueeTextBlock : UserControl
    {
        private readonly DispatcherTimer _timer;
        private double _offset;

        public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
            nameof(Text),
            typeof(string),
            typeof(MarqueeTextBlock),
            new PropertyMetadata(string.Empty, OnTextChanged));

        public MarqueeTextBlock()
        {
            InitializeComponent();
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(30) };
            _timer.Tick += Timer_Tick;
            MouseEnter += (sender, e) => StartScrolling();
            MouseLeave += (sender, e) => StopScrolling();
            SizeChanged += (sender, e) =>
            {
                if (!_timer.IsEnabled)
                {
                    Scroller.ScrollToHorizontalOffset(0);
                }
            };
        }

        public string Text
        {
            get { return (string)GetValue(TextProperty); }
            set { SetValue(TextProperty, value); }
        }

        private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (MarqueeTextBlock)d;
            control.NoteText.Text = e.NewValue as string ?? string.Empty;
            control._offset = 0;
            control.Scroller.ScrollToHorizontalOffset(0);
        }

        private void StartScrolling()
        {
            if (_timer.IsEnabled || NoteText.ActualWidth <= ActualWidth)
            {
                return;
            }

            _offset = 0;
            Scroller.ScrollToHorizontalOffset(0);
            _timer.Start();
        }

        private void StopScrolling()
        {
            if (_timer.IsEnabled)
            {
                _timer.Stop();
            }

            _offset = 0;
            Scroller.ScrollToHorizontalOffset(0);
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            var maxOffset = NoteText.ActualWidth - ActualWidth;
            if (maxOffset <= 0)
            {
                StopScrolling();
                return;
            }

            _offset += 1.2;
            if (_offset >= maxOffset + 24)
            {
                _offset = 0;
            }

            Scroller.ScrollToHorizontalOffset(Math.Min(_offset, maxOffset));
        }
    }
}

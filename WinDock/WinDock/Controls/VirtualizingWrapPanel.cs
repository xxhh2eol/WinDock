using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace WinDock.Controls
{
    /// <summary>
    /// 支持虚拟化的换行面板：只实例化可视区域内的子元素，滚动时回收复用。
    /// 不依赖 IScrollInfo——直接读取祖先 ScrollViewer 的视口与偏移（像素滚动），
    /// 通过订阅 ScrollChanged 在滚动时重新实例化。
    /// </summary>
    public sealed class VirtualizingWrapPanel : VirtualizingPanel
    {
        private Size _childSize = new Size(0, 0);
        private int _columns = 1;
        private int _firstVisibleIndex = 0;
        private int _lastVisibleIndex = -1;
        private ScrollViewer _scrollViewer;
        private bool _subscribed;

        public VirtualizingWrapPanel()
        {
            // 面板首次测量时生成器可能尚未接线（ItemsControl 初始化中），
            // Loaded 时强制重测一次，确保能正常生成容器。
            Loaded += (sender, e) => InvalidateMeasure();
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            var generator = ItemContainerGenerator;
            var children = InternalChildren;
            var owner = ItemsControl.GetItemsOwner(this);
            var itemCount = owner != null ? owner.Items.Count : 0;

            if (itemCount == 0 || generator == null)
            {
                // 条目为空或生成器尚未接线（ItemsControl 初始化中），
                // 安全返回，等 OnItemsChanged / 后续测量再生成。
                CleanupItems(itemCount);
                return new Size(0, 0);
            }

            // 用第一个已实例化子元素（或临时实例化一个）确定统一的子元素尺寸。
            if (children.Count > 0)
            {
                var first = children[0];
                first.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                _childSize = first.DesiredSize;
            }
            else
            {
                using (generator.StartAt(generator.GeneratorPositionFromIndex(0), GeneratorDirection.Forward, true))
                {
                    var prototype = generator.GenerateNext() as UIElement;
                    if (prototype == null)
                    {
                        return new Size(0, 0);
                    }

                    AddInternalChild(prototype);
                    generator.PrepareItemContainer(prototype);
                    prototype.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    _childSize = prototype.DesiredSize;
                }

                if (_childSize.Width <= 0 || _childSize.Height <= 0)
                {
                    return new Size(0, 0);
                }

                // 原型只用于探测尺寸，测完即从生成器与子元素中移除，
                // 保证 RealizeRange 从干净状态按规范位置生成，避免索引错位。
                RemoveInternalChildRange(0, 1);
                generator.Remove(generator.GeneratorPositionFromIndex(0), 1);
            }

            EnsureScrollViewerSubscription();

            // 视口尺寸：宽取测量约束（有限），高取祖先 ScrollViewer 的视口。
            var viewportWidth = double.IsInfinity(availableSize.Width) || availableSize.Width <= 0
                ? (_scrollViewer != null && _scrollViewer.ViewportWidth > 0 ? _scrollViewer.ViewportWidth : 1)
                : availableSize.Width;
            var viewportHeight = _scrollViewer != null && _scrollViewer.ViewportHeight > 0
                ? _scrollViewer.ViewportHeight
                : (double.IsInfinity(availableSize.Height) || availableSize.Height <= 0 ? 1 : availableSize.Height);

            _columns = Math.Max(1, (int)(viewportWidth / _childSize.Width));
            var rowCount = (int)Math.Ceiling((double)itemCount / _columns);
            var extentHeight = rowCount * _childSize.Height;

            var scrollOffset = _scrollViewer != null ? _scrollViewer.VerticalOffset : 0;

            // 计算可视范围内的子元素区间。
            var firstRow = (int)(scrollOffset / _childSize.Height);
            var visibleRows = (int)Math.Ceiling(viewportHeight / _childSize.Height) + 1;
            _firstVisibleIndex = Math.Max(0, firstRow * _columns);
            _lastVisibleIndex = Math.Min(itemCount - 1, _firstVisibleIndex + visibleRows * _columns - 1);

            RealizeRange(generator, _firstVisibleIndex, _lastVisibleIndex);
            CleanupItems(itemCount);

            // 返回完整范围高度，让 ScrollViewer 显示滚动条并按像素滚动。
            var desired = new Size(viewportWidth, Math.Max(extentHeight, viewportHeight));
            return new Size(
                double.IsInfinity(desired.Width) || double.IsNaN(desired.Width) ? 0 : desired.Width,
                double.IsInfinity(desired.Height) || double.IsNaN(desired.Height) ? 0 : desired.Height);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var children = InternalChildren;
            for (var i = 0; i < children.Count; i++)
            {
                var itemIndex = _firstVisibleIndex + i;
                var row = itemIndex / _columns;
                var column = itemIndex % _columns;
                var x = column * _childSize.Width;
                var y = row * _childSize.Height;
                children[i].Arrange(new Rect(new Point(x, y), _childSize));
            }

            return finalSize;
        }

        private void EnsureScrollViewerSubscription()
        {
            var host = FindAncestor<ScrollViewer>(this);
            if (host == _scrollViewer)
            {
                return;
            }

            if (_scrollViewer != null && _subscribed)
            {
                _scrollViewer.ScrollChanged -= ScrollViewer_ScrollChanged;
            }

            _scrollViewer = host;
            _subscribed = false;
            if (host != null)
            {
                host.ScrollChanged += ScrollViewer_ScrollChanged;
                _subscribed = true;
            }
        }

        private void ScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            InvalidateMeasure();
        }

        protected override void OnItemsChanged(object sender, ItemsChangedEventArgs args)
        {
            base.OnItemsChanged(sender, args);
            InvalidateMeasure();
        }

        private static T FindAncestor<T>(DependencyObject current) where T : DependencyObject
        {
            while (current != null)
            {
                var match = current as T;
                if (match != null)
                {
                    return match;
                }

                current = VisualTreeHelper.GetParent(current);
            }

            return null;
        }

        private void RealizeRange(IItemContainerGenerator generator, int first, int last)
        {
            if (first > last)
            {
                return;
            }

            var startPosition = generator.GeneratorPositionFromIndex(first);
            var childIndex = startPosition.Offset == 0 ? startPosition.Index : startPosition.Index + 1;

            using (generator.StartAt(startPosition, GeneratorDirection.Forward, true))
            {
                for (var itemIndex = first; itemIndex <= last; itemIndex++, childIndex++)
                {
                    var child = generator.GenerateNext() as UIElement;
                    if (child == null)
                    {
                        continue;
                    }

                    if (childIndex >= InternalChildren.Count)
                    {
                        AddInternalChild(child);
                    }
                    else if (InternalChildren[childIndex] != child)
                    {
                        InsertInternalChild(childIndex, child);
                    }

                    generator.PrepareItemContainer(child);
                }
            }
        }

        private void CleanupItems(int itemCount)
        {
            var generator = ItemContainerGenerator;
            var children = InternalChildren;

            for (var i = children.Count - 1; i >= 0; i--)
            {
                var itemIndex = _firstVisibleIndex + i;
                if (itemIndex > _lastVisibleIndex || itemIndex >= itemCount)
                {
                    var position = generator.GeneratorPositionFromIndex(itemIndex);
                    generator.Remove(position, 1);
                    RemoveInternalChildRange(i, 1);
                }
            }
        }
    }
}

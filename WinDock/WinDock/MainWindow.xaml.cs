using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using WinDock.Models;
using WinDock.Services;

namespace WinDock
{
    [SupportedOSPlatform("windows")]
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private readonly DockCatalogService _catalogService;
        private DockStore _store;

        public MainWindow()
        {
            InitializeComponent();
            _catalogService = new DockCatalogService();
            DefaultItems = new ObservableCollection<DockItem>();
            MoreItems = new ObservableCollection<DockItem>();
            HiddenItems = new ObservableCollection<DockItem>();
            DataContext = this;
            Loaded += MainWindow_Loaded;
            Closing += MainWindow_Closing;
        }

        public ObservableCollection<DockItem> DefaultItems { get; private set; }
        public ObservableCollection<DockItem> MoreItems { get; private set; }
        public ObservableCollection<DockItem> HiddenItems { get; private set; }

        public double IconSize
        {
            get { return _store == null || _store.IconSize < 24 ? 64 : _store.IconSize; }
            set
            {
                var normalized = Math.Max(24, Math.Min(128, value));
                if (_store == null || Math.Abs(_store.IconSize - normalized) < 0.1)
                {
                    return;
                }

                _store.IconSize = normalized;
                _catalogService.Save(_store);
                OnPropertyChanged(nameof(IconSize));
                OnPropertyChanged(nameof(IconScale));
            }
        }

        public double IconScale
        {
            get { return Math.Max(24, Math.Min(128, IconSize)) / 64; }
        }

        private FontFamily _appFontFamily = new FontFamily("Segoe UI");

        public FontFamily AppFontFamily
        {
            get { return _appFontFamily; }
            set
            {
                _appFontFamily = value;
                OnPropertyChanged(nameof(AppFontFamily));
            }
        }

        public double IconOpacity
        {
            get { return _store == null ? 1 : _store.IconOpacity; }
        }

        public double HiddenIconOpacity
        {
            get { return IconOpacity * 0.65; }
        }

        public bool WindowShadow
        {
            get { return _store == null || _store.WindowShadow; }
            set
            {
                if (_store == null || _store.WindowShadow == value)
                {
                    return;
                }

                _store.WindowShadow = value;
                _catalogService.Save(_store);
                OnPropertyChanged(nameof(WindowShadow));
                ApplyWindowShadow();
            }
        }

        public bool UseVirtualization
        {
            get { return _store == null || _store.UseVirtualization; }
            set
            {
                if (_store == null || _store.UseVirtualization == value)
                {
                    return;
                }

                _store.UseVirtualization = value;
                _catalogService.Save(_store);
                OnPropertyChanged(nameof(UseVirtualization));
            }
        }

        private static readonly DropShadowEffect WindowShadowEffect = CreateWindowShadowEffect();

        private static DropShadowEffect CreateWindowShadowEffect()
        {
            var effect = new DropShadowEffect
            {
                BlurRadius = 24,
                ShadowDepth = 8,
                Opacity = 0.18,
                Color = Color.FromRgb(0x33, 0x41, 0x55)
            };
            effect.Freeze();
            return effect;
        }

        private void ApplyWindowShadow()
        {
            WindowSurfaceBorder.Effect = WindowShadow ? WindowShadowEffect : null;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Collections.Generic.IList<DockItem> newItems;
                _store = _catalogService.LoadAndRefresh(out newItems);
                RefreshCollections();
                OnPropertyChanged(nameof(IconSize));
                OnPropertyChanged(nameof(IconScale));
                OnPropertyChanged(nameof(IconOpacity));
                OnPropertyChanged(nameof(HiddenIconOpacity));
                OnPropertyChanged(nameof(WindowShadow));
                OnPropertyChanged(nameof(UseVirtualization));
                ApplyWindowShadow();
                RestoreWindowState();
                IconSizeSlider.Value = IconSize;
                IconOpacitySlider.Value = Math.Max(IconOpacitySlider.Minimum, Math.Min(IconOpacitySlider.Maximum, IconOpacity * 100));
                WindowOpacitySlider.Value = Math.Max(WindowOpacitySlider.Minimum, Math.Min(WindowOpacitySlider.Maximum, _store.WindowOpacity * 100));
                SortModeComboBox.SelectedIndex = Math.Max(0, Math.Min(SortModeComboBox.Items.Count - 1, _store.SortMode));
                Opacity = _store.WindowOpacity;
                // PopulateFontComboBox(_store.FontFamilyName);
                // ApplyFont(_store.FontFamilyName);

                if (newItems.Count > 0)
                {
                    MessageBox.Show(
                        string.Format("发现 {0} 个新应用，已添加到“更多应用”页面。", newItems.Count),
                        "WinDock",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("读取 WinDock 图标列表失败：" + ex.Message, "WinDock", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void IconSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (e.NewValue < 24 || e.NewValue > 128)
            {
                return;
            }

            if (_store != null)
            {
                _store.IconSize = e.NewValue;
                _catalogService.Save(_store);
                OnPropertyChanged(nameof(IconSize));
                OnPropertyChanged(nameof(IconScale));
            }
        }

        /* 字体修改功能暂时注释（保留代码，便于后续恢复）。
        private void PopulateFontComboBox(string fontName)
        {
            var families = Fonts.SystemFontFamilies
                .OrderBy(family => family.Source, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            if (!string.IsNullOrWhiteSpace(fontName)
                && families.All(family => !string.Equals(family.Source, fontName, StringComparison.OrdinalIgnoreCase)))
            {
                families.Add(new FontFamily(fontName));
                families = families.OrderBy(family => family.Source, StringComparer.CurrentCultureIgnoreCase).ToList();
            }

            FontFamilyComboBox.ItemsSource = families;
            FontFamilyComboBox.SelectedItem = families.FirstOrDefault(family =>
                string.Equals(family.Source, fontName ?? string.Empty, StringComparison.OrdinalIgnoreCase));
        }

        private void FontFamilyComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_store == null)
            {
                return;
            }

            var selected = FontFamilyComboBox.SelectedItem as FontFamily;
            if (selected == null || string.IsNullOrWhiteSpace(selected.Source))
            {
                return;
            }

            _store.FontFamilyName = selected.Source;
            _catalogService.Save(_store);
            ApplyFont(selected.Source);
        }

        private void ApplyFont(string fontName)
        {
            FontFamily family;
            try
            {
                family = new FontFamily(string.IsNullOrWhiteSpace(fontName) ? "Segoe UI" : fontName);
            }
            catch (Exception)
            {
                family = new FontFamily("Segoe UI");
            }

            AppFontFamily = family;
            FontFamily = family;
            if (FontPreviewText != null)
            {
                FontPreviewText.FontFamily = family;
            }
        }
        */

        private void RefreshList_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                IList<DockItem> newItems;
                _store = _catalogService.LoadAndRefresh(out newItems);
                RefreshCollections();
                OnPropertyChanged(nameof(IconSize));
                OnPropertyChanged(nameof(IconScale));
                OnPropertyChanged(nameof(IconOpacity));
                OnPropertyChanged(nameof(HiddenIconOpacity));
                IconSizeSlider.Value = IconSize;
                IconOpacitySlider.Value = Math.Max(IconOpacitySlider.Minimum, Math.Min(IconOpacitySlider.Maximum, IconOpacity * 100));
                WindowOpacitySlider.Value = Math.Max(WindowOpacitySlider.Minimum, Math.Min(WindowOpacitySlider.Maximum, _store.WindowOpacity * 100));
                SortModeComboBox.SelectedIndex = Math.Max(0, Math.Min(SortModeComboBox.Items.Count - 1, _store.SortMode));
                Opacity = _store.WindowOpacity;
                // PopulateFontComboBox(_store.FontFamilyName);
                // ApplyFont(_store.FontFamilyName);

                if (newItems.Count > 0)
                {
                    MessageBox.Show(
                        string.Format("发现 {0} 个新应用，已添加到“更多应用”页面。", newItems.Count),
                        "WinDock",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("刷新图标列表失败：" + ex.Message, "WinDock", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void IconOpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_store == null)
            {
                return;
            }

            _store.IconOpacity = Math.Max(0, Math.Min(100, e.NewValue)) / 100.0;
            _catalogService.Save(_store);
            OnPropertyChanged(nameof(IconOpacity));
            OnPropertyChanged(nameof(HiddenIconOpacity));
            if (IconOpacityValueText != null)
            {
                IconOpacityValueText.Text = (int)Math.Round(e.NewValue) + "%";
            }
        }

        private void WindowOpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_store == null)
            {
                return;
            }

            _store.WindowOpacity = Math.Max(30, Math.Min(100, e.NewValue)) / 100.0;
            _catalogService.Save(_store);
            Opacity = _store.WindowOpacity;
            if (WindowOpacityValueText != null)
            {
                WindowOpacityValueText.Text = (int)Math.Round(e.NewValue) + "%";
            }
        }

        private static System.Collections.Generic.IEnumerable<T> FindVisualChildren<T>(DependencyObject root) where T : DependencyObject
        {
            if (root == null)
            {
                yield break;
            }

            for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
            {
                var child = VisualTreeHelper.GetChild(root, index);
                var match = child as T;
                if (match != null)
                {
                    yield return match;
                }

                foreach (var descendant in FindVisualChildren<T>(child))
                {
                    yield return descendant;
                }
            }
        }

        private void RestoreWindowState()
        {
            if (_store.WindowWidth <= 0 || _store.WindowHeight <= 0)
            {
                Left = SystemParameters.WorkArea.Left + (SystemParameters.WorkArea.Width - Width) / 2;
                Top = SystemParameters.WorkArea.Top + (SystemParameters.WorkArea.Height - Height) / 2;
                return;
            }

            Width = _store.WindowWidth;
            Height = _store.WindowHeight;
            var workArea = SystemParameters.WorkArea;
            Left = Math.Max(workArea.Left, Math.Min(_store.WindowLeft, workArea.Right - Width));
            Top = Math.Max(workArea.Top, Math.Min(_store.WindowTop, workArea.Bottom - Height));
            if (string.Equals(_store.WindowState, "Maximized", StringComparison.OrdinalIgnoreCase))
            {
                WindowState = WindowState.Maximized;
            }
        }

        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            if (_store == null)
            {
                return;
            }

            var bounds = WindowState == WindowState.Normal ? new Rect(Left, Top, Width, Height) : RestoreBounds;
            _store.WindowLeft = bounds.Left;
            _store.WindowTop = bounds.Top;
            _store.WindowWidth = bounds.Width;
            _store.WindowHeight = bounds.Height;
            _store.WindowState = WindowState.ToString();
            _catalogService.Save(_store);
        }

        private Point _dragStartScreen;
        private bool _dragArmed;

        private DockItem _dragItem;
        private Border _dragSourceBorder;
        private Point _dragStartPoint;
        private bool _draggingTile;

        private void Root_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left || e.ButtonState != MouseButtonState.Pressed)
            {
                return;
            }

            var source = e.OriginalSource as DependencyObject;
            if (IsInteractiveElement(source))
            {
                // 磁贴上按下：若排序方式为"默认"，记录拖动起点（拖动排序）。
                if (_store != null && (DockSortMode)_store.SortMode == DockSortMode.Default)
                {
                    var tile = FindDockItemBorder(source);
                    if (tile != null)
                    {
                        _dragItem = tile.Tag as DockItem;
                        _dragSourceBorder = tile;
                        _dragStartPoint = e.GetPosition(this);
                        _draggingTile = false;
                    }
                }

                return;
            }

            if (e.ClickCount == 2)
            {
                ToggleMaximize();
                e.Handled = true;
                return;
            }

            if (WindowState == WindowState.Maximized)
            {
                // 最大化时先等鼠标移动再还原拖动，避免双击还原被第一次点击破坏。
                _dragStartScreen = PointToScreen(e.GetPosition(this));
                _dragArmed = true;
                MouseMove += Root_DragMouseMove;
                MouseLeftButtonUp += Root_DragMouseLeftButtonUp;
            }
            else
            {
                try
                {
                    DragMove();
                }
                catch (InvalidOperationException)
                {
                    // 拖动期间窗口状态变化导致拖动失败时忽略。
                }
            }
        }

        private void Root_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (_dragItem == null || _draggingTile || e.LeftButton != MouseButtonState.Pressed)
            {
                return;
            }

            var position = e.GetPosition(this);
            if (Math.Abs(position.X - _dragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance
                && Math.Abs(position.Y - _dragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
            {
                return;
            }

            _draggingTile = true;
            if (_dragSourceBorder != null)
            {
                Mouse.Capture(_dragSourceBorder);
                _dragSourceBorder.Opacity = 0.55;
            }
        }

        private void Root_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            var dragged = _dragItem;
            var dragging = _draggingTile;
            _dragItem = null;
            _draggingTile = false;
            if (_dragSourceBorder != null)
            {
                if (dragging)
                {
                    _dragSourceBorder.Opacity = 1;
                }

                Mouse.Capture(null);
                _dragSourceBorder = null;
            }

            if (!dragging || dragged == null)
            {
                return;
            }

            var dropBorder = FindDropTarget(e.GetPosition(this));
            var target = dropBorder == null ? null : dropBorder.Tag as DockItem;
            if (target != null && !ReferenceEquals(target, dragged))
            {
                MoveItem(dragged, target);
            }
        }

        private static Border FindDockItemBorder(DependencyObject source)
        {
            var current = source;
            while (current != null)
            {
                var border = current as Border;
                if (border != null && border.Tag is DockItem)
                {
                    return border;
                }

                current = VisualTreeHelper.GetParent(current);
            }

            return null;
        }

        private void Root_DragMouseMove(object sender, MouseEventArgs e)
        {
            if (!_dragArmed || e.LeftButton != MouseButtonState.Pressed)
            {
                return;
            }

            var current = PointToScreen(e.GetPosition(this));
            if (Math.Abs(current.X - _dragStartScreen.X) < 4 && Math.Abs(current.Y - _dragStartScreen.Y) < 4)
            {
                return;
            }

            _dragArmed = false;
            MouseMove -= Root_DragMouseMove;
            MouseLeftButtonUp -= Root_DragMouseLeftButtonUp;

            var grabPoint = _dragStartScreen;
            var relativeX = (grabPoint.X - Left) / Math.Max(1, ActualWidth);
            var relativeY = (grabPoint.Y - Top) / Math.Max(1, ActualHeight);
            WindowState = WindowState.Normal;
            Left = grabPoint.X - ActualWidth * relativeX;
            Top = grabPoint.Y - ActualHeight * relativeY;

            try
            {
                DragMove();
            }
            catch (InvalidOperationException)
            {
                // 拖动期间窗口状态变化导致拖动失败时忽略。
            }
        }

        private void Root_DragMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _dragArmed = false;
            MouseMove -= Root_DragMouseMove;
            MouseLeftButtonUp -= Root_DragMouseLeftButtonUp;
        }

        private static bool IsInteractiveElement(DependencyObject source)
        {
            var current = source;
            while (current != null)
            {
                if (current is ButtonBase || current is Slider || current is ComboBox || current is TabItem
                    || current is ScrollBar || current is Thumb || current is MenuItem || current is CheckBox
                    || current is RadioButton)
                {
                    return true;
                }

                var border = current as Border;
                if (border != null && border.Tag is DockItem)
                {
                    return true;
                }

                var element = current as FrameworkElement;
                if (element != null && element.Name == "IconSizePanel")
                {
                    return true;
                }

                current = VisualTreeHelper.GetParent(current);
            }

            return false;
        }

        private void ToggleMaximize()
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void RefreshCollections()
        {
            var defaults = new List<DockItem>();
            var more = new List<DockItem>();
            var hidden = new List<DockItem>();

            foreach (var item in _store.Items.Where(item => !item.IsUnavailable))
            {
                if (item.Group == DockItemGroup.Default)
                {
                    defaults.Add(item);
                }
                else if (item.Group == DockItemGroup.Hidden)
                {
                    hidden.Add(item);
                }
                else
                {
                    more.Add(item);
                }
            }

            EnsureOrders(defaults);
            EnsureOrders(more);
            EnsureOrders(hidden);
            ApplySort(defaults);
            ApplySort(more);
            ApplySort(hidden);

            DefaultItems.Clear();
            MoreItems.Clear();
            HiddenItems.Clear();
            foreach (var item in defaults)
            {
                DefaultItems.Add(item);
            }

            foreach (var item in more)
            {
                MoreItems.Add(item);
            }

            foreach (var item in hidden)
            {
                HiddenItems.Add(item);
            }
        }

        /// <summary>给没有序号的条目补序号（新添加的、或旧存档），并持久化。</summary>
        private void EnsureOrders(List<DockItem> items)
        {
            var max = 0.0;
            foreach (var item in items)
            {
                if (item.Order > max)
                {
                    max = item.Order;
                }
            }

            var changed = false;
            foreach (var item in items)
            {
                if (item.Order <= 0)
                {
                    item.Order = ++max;
                    changed = true;
                }
            }

            if (changed && _store != null)
            {
                _catalogService.Save(_store);
            }
        }

        private void ApplySort(List<DockItem> items)
        {
            switch ((DockSortMode)_store.SortMode)
            {
                case DockSortMode.NameAsc:
                    items.Sort((a, b) => string.Compare(a.DisplayName, b.DisplayName, StringComparison.CurrentCultureIgnoreCase));
                    break;
                case DockSortMode.NameDesc:
                    items.Sort((a, b) => string.Compare(b.DisplayName, a.DisplayName, StringComparison.CurrentCultureIgnoreCase));
                    break;
                case DockSortMode.InstallAsc:
                    items.Sort((a, b) => GetInstallTime(a).CompareTo(GetInstallTime(b)));
                    break;
                case DockSortMode.InstallDesc:
                    items.Sort((a, b) => GetInstallTime(b).CompareTo(GetInstallTime(a)));
                    break;
                default:
                    items.Sort((a, b) => a.Order.CompareTo(b.Order));
                    break;
            }
        }

        private readonly Dictionary<string, DateTime> _installTimeCache = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);

        /// <summary>安装时间近似：目标程序文件（或快捷方式）的创建时间，按条目缓存。</summary>
        private DateTime GetInstallTime(DockItem item)
        {
            DateTime time;
            if (_installTimeCache.TryGetValue(item.Id, out time))
            {
                return time;
            }

            try
            {
                var target = DockDiscoveryService.ResolveTargetPath(item.TargetPath) ?? item.TargetPath;
                if (File.Exists(target))
                {
                    time = File.GetCreationTime(target);
                }
                else if (Directory.Exists(target))
                {
                    time = Directory.GetCreationTime(target);
                }
                else
                {
                    time = DateTime.MinValue;
                }
            }
            catch (Exception)
            {
                time = DateTime.MinValue;
            }

            _installTimeCache[item.Id] = time;
            return time;
        }

        private void SortModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_store == null)
            {
                return;
            }

            var selected = SortModeComboBox.SelectedItem as ComboBoxItem;
            if (selected == null || selected.Tag == null)
            {
                return;
            }

            DockSortMode mode;
            if (Enum.TryParse((string)selected.Tag, out mode))
            {
                _store.SortMode = (int)mode;
                _catalogService.Save(_store);
                RefreshCollections();
            }
        }

        private void Icon_PreviewRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            var border = sender as FrameworkElement;
            var item = border == null ? null : border.Tag as DockItem;
            if (item == null)
            {
                return;
            }

            var menu = CreateContextMenu(item);
            border.ContextMenu = menu;
            menu.IsOpen = true;
            e.Handled = true;
        }

        private Border FindDropTarget(Point windowPosition)
        {
            var hit = InputHitTest(windowPosition) as DependencyObject;
            while (hit != null)
            {
                var border = hit as Border;
                if (border != null && border.Tag is DockItem)
                {
                    return border;
                }

                hit = VisualTreeHelper.GetParent(hit);
            }

            return null;
        }

        private void MoveItem(DockItem dragged, DockItem target)
        {
            ObservableCollection<DockItem> list;
            if (dragged.Group == DockItemGroup.Default)
            {
                list = DefaultItems;
            }
            else if (dragged.Group == DockItemGroup.Hidden)
            {
                list = HiddenItems;
            }
            else
            {
                list = MoreItems;
            }

            var oldIndex = list.IndexOf(dragged);
            var newIndex = list.IndexOf(target);
            if (oldIndex < 0 || newIndex < 0 || oldIndex == newIndex)
            {
                return;
            }

            list.RemoveAt(oldIndex);
            list.Insert(newIndex, dragged);

            for (var i = 0; i < list.Count; i++)
            {
                list[i].Order = i + 1;
            }

            _catalogService.Save(_store);
        }

        private ContextMenu CreateContextMenu(DockItem item)
        {
            var menu = new ContextMenu { Tag = item, FontFamily = FontFamily };
            if (item.Group != DockItemGroup.Default)
            {
                menu.Items.Add(CreateMenuItem("移到默认", MoveToDefault_Click));
            }
            if (item.Group != DockItemGroup.More)
            {
                menu.Items.Add(CreateMenuItem("移到更多", MoveToMore_Click));
            }
            if (item.Group != DockItemGroup.Hidden)
            {
                menu.Items.Add(CreateMenuItem("隐藏", HideItem_Click));
            }
            menu.Items.Add(new Separator());
            menu.Items.Add(CreateMenuItem(string.IsNullOrWhiteSpace(item.Note) ? "添加备注…" : "编辑备注…", EditNote_Click));
            if (!string.IsNullOrWhiteSpace(item.Note))
            {
                menu.Items.Add(CreateMenuItem("清除备注", ClearNote_Click));
            }
            menu.Items.Add(new Separator());
            menu.Items.Add(CreateMenuItem("删除", DeleteItem_Click));
            return menu;
        }

        private static MenuItem CreateMenuItem(string header, RoutedEventHandler handler)
        {
            var item = new MenuItem { Header = header };
            item.Click += handler;
            return item;
        }

        private void MoveToDefault_Click(object sender, RoutedEventArgs e)
        {
            ChangeItemGroup(sender, DockItemGroup.Default);
        }

        private void MoveToMore_Click(object sender, RoutedEventArgs e)
        {
            ChangeItemGroup(sender, DockItemGroup.More);
        }

        private void HideItem_Click(object sender, RoutedEventArgs e)
        {
            ChangeItemGroup(sender, DockItemGroup.Hidden);
        }

        private void ChangeItemGroup(object sender, DockItemGroup group)
        {
            var menuItem = sender as MenuItem;
            var menu = menuItem == null ? null : menuItem.Parent as ContextMenu;
            var item = menu == null ? null : menu.Tag as DockItem;
            if (item == null)
            {
                return;
            }

            item.Group = group;
            _catalogService.Save(_store);
            RefreshCollections();
        }

        private void DeleteItem_Click(object sender, RoutedEventArgs e)
        {
            var element = sender as FrameworkElement;
            var item = element == null ? null : element.Tag as DockItem;
            if (item == null)
            {
                var menuItem = sender as MenuItem;
                var menu = menuItem == null ? null : menuItem.Parent as ContextMenu;
                item = menu == null ? null : menu.Tag as DockItem;
            }
            if (item == null)
            {
                return;
            }

            _catalogService.Remove(_store, item);
            RefreshCollections();
        }

        private void EditNote_Click(object sender, RoutedEventArgs e)
        {
            var item = GetContextItem(sender);
            if (item == null)
            {
                return;
            }

            string note;
            if (!ShowNoteDialog(item.Note ?? string.Empty, out note))
            {
                return;
            }

            item.Note = note;
            _catalogService.Save(_store);
        }

        private void ClearNote_Click(object sender, RoutedEventArgs e)
        {
            var item = GetContextItem(sender);
            if (item == null)
            {
                return;
            }

            item.Note = string.Empty;
            _catalogService.Save(_store);
        }

        private static DockItem GetContextItem(object sender)
        {
            var menuItem = sender as MenuItem;
            var menu = menuItem == null ? null : menuItem.Parent as ContextMenu;
            return menu == null ? null : menu.Tag as DockItem;
        }

        private bool ShowNoteDialog(string current, out string note)
        {
            note = string.Empty;
            string result = string.Empty;
            var dialog = new Window
            {
                Title = "添加备注",
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                WindowStyle = WindowStyle.ToolWindow,
                ResizeMode = ResizeMode.NoResize,
                ShowInTaskbar = false,
                SizeToContent = SizeToContent.WidthAndHeight
            };

            var panel = new StackPanel { Margin = new Thickness(16), MinWidth = 320 };
            panel.Children.Add(new TextBlock
            {
                Text = "备注（最多 20 字）：",
                Margin = new Thickness(0, 0, 0, 8),
                Foreground = new SolidColorBrush(Color.FromRgb(0x47, 0x55, 0x69))
            });

            var input = new TextBox
            {
                MaxLength = 20,
                Text = current,
                FontSize = 14,
                MinWidth = 280,
                Padding = new Thickness(4)
            };
            panel.Children.Add(input);

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 14, 0, 0)
            };
            var okButton = new Button
            {
                Content = "确定",
                Width = 76,
                Margin = new Thickness(0, 0, 8, 0),
                IsDefault = true,
                Padding = new Thickness(10, 4, 10, 4)
            };
            okButton.Click += (sender2, e2) =>
            {
                result = input.Text.Trim();
                dialog.DialogResult = true;
            };
            var cancelButton = new Button
            {
                Content = "取消",
                Width = 76,
                IsCancel = true,
                Padding = new Thickness(10, 4, 10, 4)
            };
            buttons.Children.Add(okButton);
            buttons.Children.Add(cancelButton);
            panel.Children.Add(buttons);

            dialog.Content = panel;
            var accepted = dialog.ShowDialog() == true;
            if (accepted)
            {
                note = result;
            }

            return accepted;
        }

        private void Item_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount < 2)
            {
                return;
            }

            var element = sender as FrameworkElement;
            LaunchItem(element == null ? null : element.Tag as DockItem);
        }

        private void LaunchItem(DockItem item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.TargetPath))
            {
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = item.TargetPath,
                    UseShellExecute = true
                });
                if (item.IsNew)
                {
                    item.IsNew = false;
                    _catalogService.Save(_store);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("打开“" + item.DisplayName + "”失败：" + ex.Message, "WinDock", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void AddFile_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "选择要添加的文件",
                Filter = "所有文件|*.*",
                CheckFileExists = true
            };

            if (dialog.ShowDialog() == true)
            {
                AddPathToGroup(dialog.FileName);
            }
        }

        private void AddFolder_Click(object sender, RoutedEventArgs e)
        {
            var folder = SelectFolder();
            if (!string.IsNullOrWhiteSpace(folder))
            {
                AddPathToGroup(folder);
            }
        }

        private void AddPathToGroup(string path)
        {
            try
            {
                var item = _catalogService.AddManual(_store, path);
                item.Group = GetSelectedGroup();
                _catalogService.Save(_store);
                RefreshCollections();
            }
            catch (Exception ex)
            {
                MessageBox.Show("添加项目失败：" + ex.Message, "WinDock", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private DockItemGroup GetSelectedGroup()
        {
            var selected = TargetGroupComboBox.SelectedItem as ComboBoxItem;
            if (selected == null)
            {
                return DockItemGroup.More;
            }

            DockItemGroup group;
            return Enum.TryParse((string)selected.Tag, out group) ? group : DockItemGroup.More;
        }

        private static string SelectFolder()
        {
            object shell = null;
            object folder = null;
            try
            {
                var shellType = Type.GetTypeFromProgID("Shell.Application");
                if (shellType == null)
                {
                    return null;
                }

                shell = Activator.CreateInstance(shellType);
                folder = shellType.InvokeMember(
                    "BrowseForFolder",
                    BindingFlags.InvokeMethod,
                    null,
                    shell,
                    new object[] { 0, "选择要添加的文件夹", 0 });
                if (folder == null)
                {
                    return null;
                }

                var self = folder.GetType().InvokeMember("Self", BindingFlags.GetProperty, null, folder, null);
                return self == null ? null : self.GetType().InvokeMember("Path", BindingFlags.GetProperty, null, self, null) as string;
            }
            catch (Exception)
            {
                return null;
            }
            finally
            {
                if (folder != null && Marshal.IsComObject(folder))
                {
                    Marshal.FinalReleaseComObject(folder);
                }
                if (shell != null && Marshal.IsComObject(shell))
                {
                    Marshal.FinalReleaseComObject(shell);
                }
            }
        }

        private void OnPropertyChanged(string propertyName)
        {
            var handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }

    public sealed class IconScaleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            var iconSize = value is double ? (double)value : 64;
            return Math.Max(24, Math.Min(128, iconSize)) / 64;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    public sealed class HasNoteConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return string.IsNullOrWhiteSpace(value as string) ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    [SupportedOSPlatform("windows")]
    public sealed class DockIconConverter : IValueConverter
    {
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, ImageSource> IconCache =
            new System.Collections.Concurrent.ConcurrentDictionary<string, ImageSource>(StringComparer.OrdinalIgnoreCase);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct ShellFileInfo
        {
            public IntPtr IconHandle;
            public int IconIndex;
            public uint Attributes;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string DisplayName;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string TypeName;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SHGetFileInfo(string path, uint fileAttributes, out ShellFileInfo fileInfo, uint fileInfoSize, uint flags);

        [DllImport("shell32.dll")]
        private static extern int SHGetImageList(int iImageList, ref Guid riid, out IntPtr ppv);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr iconHandle);

        [DllImport("comctl32.dll")]
        private static extern IntPtr ImageList_GetIcon(IntPtr himl, int i, uint flags);

        private const uint ShgfiIcon = 0x000000100;
        private const uint ShgfiUseFileAttributes = 0x000000010;
        private const uint ShgfiSysIconIndex = 0x000004000;
        private const uint FileAttributeNormal = 0x00000080;
        private const int ShilJumbo = 0x4; // 256x256 大图标
        private const uint IldTransparent = 0x1;

        private static readonly Guid IidImageList = new Guid("46EB5926-582E-4017-9FDF-E8998DAA0950");

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            var path = value as string;
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path) && !Directory.Exists(path))
            {
                return null;
            }

            ImageSource cached;
            if (IconCache.TryGetValue(path, out cached))
            {
                return cached;
            }

            var icon = ExtractIcon(path);
            if (icon != null)
            {
                IconCache.TryAdd(path, icon);
            }

            return icon;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotSupportedException();
        }

        public static void ClearCache()
        {
            IconCache.Clear();
        }

        private static ImageSource ExtractIcon(string path)
        {
            try
            {
                var iconPath = ResolveShortcutTarget(path) ?? path;

                // 首选：从系统图标列表取 256x256 大图标，放大显示依然清晰。
                IntPtr imageList;
                var imageListIid = IidImageList;
                if (SHGetImageList(ShilJumbo, ref imageListIid, out imageList) == 0 && imageList != IntPtr.Zero)
                {
                    ShellFileInfo fileInfo;
                    var indexResult = SHGetFileInfo(iconPath, FileAttributeNormal, out fileInfo, (uint)Marshal.SizeOf(typeof(ShellFileInfo)), ShgfiSysIconIndex);
                    if (indexResult != IntPtr.Zero && fileInfo.IconIndex >= 0)
                    {
                        var hIcon = ImageList_GetIcon(imageList, fileInfo.IconIndex, IldTransparent);
                        if (hIcon != IntPtr.Zero)
                        {
                            try
                            {
                                var source = Imaging.CreateBitmapSourceFromHIcon(hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                                source.Freeze();
                                return source;
                            }
                            finally
                            {
                                DestroyIcon(hIcon);
                            }
                        }
                    }
                }

                // 回退：SHGetFileInfo 直接取图标（32/48px）。
                ShellFileInfo fileInfo2;
                var result = SHGetFileInfo(iconPath, FileAttributeNormal, out fileInfo2, (uint)Marshal.SizeOf(typeof(ShellFileInfo)), ShgfiIcon | ShgfiUseFileAttributes);
                if (result == IntPtr.Zero || fileInfo2.IconHandle == IntPtr.Zero)
                {
                    return null;
                }

                try
                {
                    var fallback = Imaging.CreateBitmapSourceFromHIcon(fileInfo2.IconHandle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                    fallback.Freeze();
                    return fallback;
                }
                finally
                {
                    DestroyIcon(fileInfo2.IconHandle);
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string ResolveShortcutTarget(string path)
        {
            var target = Services.DockDiscoveryService.ResolveTargetPath(path);
            if (string.IsNullOrWhiteSpace(target)
                || string.Equals(target, path, StringComparison.OrdinalIgnoreCase)
                || !File.Exists(target))
            {
                return null;
            }

            return target;
        }
    }
}

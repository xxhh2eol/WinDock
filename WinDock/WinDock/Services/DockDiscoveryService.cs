using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using WinDock.Models;

namespace WinDock.Services
{
    public sealed class DockDiscoveryService
    {
        public IList<DockItem> Discover()
        {
            var items = new Dictionary<string, DockItem>(StringComparer.OrdinalIgnoreCase);

            AddFiles(items, Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), DockItemSource.Desktop);
            AddFiles(items, Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), DockItemSource.StartMenu);
            AddFiles(items, Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), DockItemSource.StartMenu);

            var taskbarPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Microsoft", "Internet Explorer", "Quick Launch", "User Pinned", "TaskBar");
            AddFiles(items, taskbarPath, DockItemSource.Taskbar);

            // 同一应用可能存在于桌面/开始菜单/任务栏等多个位置（多个 .lnk），
            // 按解析出的真实目标去重，只保留优先级最高的一份。
            var byTarget = new Dictionary<string, DockItem>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in items.Values)
            {
                var target = ResolveTargetPath(item.TargetPath) ?? item.TargetPath;
                DockItem existing;
                if (!byTarget.TryGetValue(target, out existing) || SourcePriority(item.Source) < SourcePriority(existing.Source))
                {
                    byTarget[target] = item;
                }
            }

            return byTarget.Values.OrderBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase).ToList();
        }

        /// <summary>
        /// 来源优先级：手动添加 &gt; 桌面 &gt; 任务栏 &gt; 开始菜单。数值越小越优先。
        /// </summary>
        public static int SourcePriority(DockItemSource source)
        {
            switch (source)
            {
                case DockItemSource.Manual:
                    return 0;
                case DockItemSource.Desktop:
                    return 1;
                case DockItemSource.Taskbar:
                    return 2;
                default:
                    return 3;
            }
        }

        /// <summary>
        /// 解析 .lnk 快捷方式指向的真实目标；非快捷方式返回原路径。
        /// </summary>
        public static string ResolveTargetPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)
                || !string.Equals(Path.GetExtension(path), ".lnk", StringComparison.OrdinalIgnoreCase)
                || !File.Exists(path))
            {
                return path;
            }

            object shell = null;
            object shortcut = null;
            try
            {
                var shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null)
                {
                    return path;
                }

                shell = Activator.CreateInstance(shellType);
                shortcut = shellType.InvokeMember("CreateShortcut", System.Reflection.BindingFlags.InvokeMethod, null, shell, new object[] { path });
                var target = shortcut.GetType().InvokeMember("TargetPath", System.Reflection.BindingFlags.GetProperty, null, shortcut, null) as string;
                return string.IsNullOrWhiteSpace(target) ? path : target;
            }
            catch (Exception)
            {
                return path;
            }
            finally
            {
                if (shortcut != null && System.Runtime.InteropServices.Marshal.IsComObject(shortcut))
                {
                    System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shortcut);
                }
                if (shell != null && System.Runtime.InteropServices.Marshal.IsComObject(shell))
                {
                    System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell);
                }
            }
        }

        private static void AddFiles(IDictionary<string, DockItem> items, string folder, DockItemSource source)
        {
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            {
                return;
            }

            foreach (var file in EnumerateFilesSafely(folder).Where(IsSupportedShortcut).Where(file => !IsUninstallShortcut(file)))
            {
                var id = NormalizePath(file);
                if (items.ContainsKey(id))
                {
                    continue;
                }

                items.Add(id, new DockItem
                {
                    Id = id,
                    DisplayName = Path.GetFileNameWithoutExtension(file),
                    TargetPath = file,
                    IconPath = file,
                    Source = source,
                    FirstSeenUtc = DateTime.UtcNow,
                    LastSeenUtc = DateTime.UtcNow,
                    IsNew = false,
                    IsUnavailable = false
                });
            }
        }

        private static IEnumerable<string> EnumerateFilesSafely(string folder)
        {
            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(folder, "*.*", SearchOption.TopDirectoryOnly);
            }
            catch (UnauthorizedAccessException)
            {
                files = Enumerable.Empty<string>();
            }
            catch (DirectoryNotFoundException)
            {
                files = Enumerable.Empty<string>();
            }
            catch (IOException)
            {
                files = Enumerable.Empty<string>();
            }

            foreach (var file in files)
            {
                yield return file;
            }

            IEnumerable<string> directories;
            try
            {
                directories = Directory.EnumerateDirectories(folder, "*", SearchOption.TopDirectoryOnly);
            }
            catch (UnauthorizedAccessException)
            {
                directories = Enumerable.Empty<string>();
            }
            catch (DirectoryNotFoundException)
            {
                directories = Enumerable.Empty<string>();
            }
            catch (IOException)
            {
                directories = Enumerable.Empty<string>();
            }

            foreach (var directory in directories)
            {
                foreach (var file in EnumerateFilesSafely(directory))
                {
                    yield return file;
                }
            }
        }

        private static bool IsSupportedShortcut(string path)
        {
            var extension = Path.GetExtension(path);
            return string.Equals(extension, ".lnk", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".url", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".exe", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".bat", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".cmd", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 过滤卸载类快捷方式（如 "Uninstall ZCode"、"卸载微信"、"uninst.exe"），避免污染图标列表。
        /// </summary>
        public static bool IsUninstallShortcut(string path)
        {
            var name = Path.GetFileNameWithoutExtension(path);
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            var lower = name.ToLowerInvariant();
            return lower.StartsWith("uninstall", StringComparison.Ordinal)
                || lower.StartsWith("uninst", StringComparison.Ordinal)
                || lower.StartsWith("卸载", StringComparison.Ordinal)
                || lower.Contains("uninstall", StringComparison.Ordinal);
        }

        public static string NormalizePath(string path)
        {
            try
            {
                return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar).ToLowerInvariant();
            }
            catch (Exception)
            {
                return path.Trim().ToLowerInvariant();
            }
        }
    }
}

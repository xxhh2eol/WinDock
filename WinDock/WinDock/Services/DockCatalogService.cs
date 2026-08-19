using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using WinDock.Models;

namespace WinDock.Services
{
    public sealed class DockCatalogService
    {
        private readonly DockStorageService _storage;
        private readonly DockDiscoveryService _discovery;

        public DockCatalogService()
            : this(new DockStorageService(), new DockDiscoveryService())
        {
        }

        public DockCatalogService(DockStorageService storage, DockDiscoveryService discovery)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _discovery = discovery ?? throw new ArgumentNullException(nameof(discovery));
        }

        public DockStore LoadAndRefresh(out IList<DockItem> newItems)
        {
            var store = _storage.Load();
            var discovered = _discovery.Discover();
            var discoveredById = discovered.ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase);
            var knownIds = new HashSet<string>(store.KnownApplicationIds ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
            var currentItems = store.Items ?? new List<DockItem>();
            var added = new List<DockItem>();

            if (!store.IsInitialized)
            {
                store.Items = discovered.ToList();
                foreach (var item in store.Items)
                {
                    item.Group = item.Source == DockItemSource.Desktop
                        ? DockItemGroup.Default
                        : DockItemGroup.More;
                }
                store.KnownApplicationIds = discovered.Select(item => item.Id).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                store.IsInitialized = true;
                store.IconSize = NormalizeIconSize(store.IconSize);
                store.LastScanUtc = DateTime.UtcNow;
                _storage.Save(store);
                newItems = added;
                return store;
            }

            foreach (var item in discovered)
            {
                var existing = currentItems.FirstOrDefault(candidate =>
                    string.Equals(candidate.Id, item.Id, StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                {
                    existing.LastSeenUtc = DateTime.UtcNow;
                    existing.IsUnavailable = false;
                    continue;
                }

                if (!knownIds.Contains(item.Id))
                {
                    item.IsNew = true;
                    currentItems.Add(item);
                    added.Add(item);
                }

                knownIds.Add(item.Id);
            }

            foreach (var item in currentItems)
            {
                if (item.Source == DockItemSource.Manual)
                {
                    item.IsUnavailable = !File.Exists(item.TargetPath);
                }
                else if (!discoveredById.ContainsKey(item.Id))
                {
                    item.IsUnavailable = !File.Exists(item.TargetPath);
                }
            }

            // 清理历史遗留的重复项：同一应用（解析后的目标相同）只保留优先级最高的一份。
            currentItems = DeduplicateByTarget(currentItems);

            // 移除历史遗留的卸载类快捷方式（如 "Uninstall Xxx"）。
            currentItems = currentItems.Where(item =>
                !DockDiscoveryService.IsUninstallShortcut(item.DisplayName)).ToList();

            store.Items = currentItems;
            store.KnownApplicationIds = knownIds.ToList();
            store.IconSize = NormalizeIconSize(store.IconSize);
            store.LastScanUtc = DateTime.UtcNow;
            _storage.Save(store);
            newItems = added;
            return store;
        }

        public void Save(DockStore store)
        {
            _storage.Save(store);
        }

        public void Remove(DockStore store, DockItem item)
        {
            if (store == null || item == null)
            {
                return;
            }

            store.Items.RemoveAll(candidate => string.Equals(candidate.Id, item.Id, StringComparison.OrdinalIgnoreCase));
            _storage.Save(store);
        }

        private static List<DockItem> DeduplicateByTarget(IEnumerable<DockItem> items)
        {
            var byTarget = new Dictionary<string, DockItem>(StringComparer.OrdinalIgnoreCase);
            var result = new List<DockItem>();
            foreach (var item in items)
            {
                var target = DockDiscoveryService.ResolveTargetPath(item.TargetPath) ?? item.TargetPath;
                DockItem existing;
                if (!byTarget.TryGetValue(target, out existing))
                {
                    byTarget[target] = item;
                    result.Add(item);
                }
                else if (DockDiscoveryService.SourcePriority(item.Source) < DockDiscoveryService.SourcePriority(existing.Source))
                {
                    // 新副本优先级更高（如桌面 > 开始菜单），替换旧副本。
                    result[result.IndexOf(existing)] = item;
                    byTarget[target] = item;
                }
            }

            return result;
        }

        public DockItem AddManual(DockStore store, string path)
        {
            if (store == null)
            {
                throw new ArgumentNullException(nameof(store));
            }

            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("图标路径不能为空。", nameof(path));
            }

            var id = DockDiscoveryService.NormalizePath(path);
            var existing = store.Items.FirstOrDefault(candidateItem =>
                string.Equals(candidateItem.Id, id, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                existing.IsUnavailable = !File.Exists(existing.TargetPath) && !Directory.Exists(existing.TargetPath);
                return existing;
            }

            var now = DateTime.UtcNow;
            var item = new DockItem
            {
                Id = id,
                DisplayName = Path.GetFileNameWithoutExtension(path),
                TargetPath = path,
                IconPath = path,
                Source = DockItemSource.Manual,
                Group = DockItemGroup.More,
                FirstSeenUtc = now,
                LastSeenUtc = now,
                IsNew = true,
                IsUnavailable = !File.Exists(path) && !Directory.Exists(path)
            };
            store.Items.Add(item);
            if (!store.KnownApplicationIds.Contains(id, StringComparer.OrdinalIgnoreCase))
            {
                store.KnownApplicationIds.Add(id);
            }
            _storage.Save(store);
            return item;
        }

        private static double NormalizeIconSize(double iconSize)
        {
            if (iconSize < 24)
            {
                return 24;
            }

            return iconSize > 128 ? 128 : iconSize;
        }
    }
}

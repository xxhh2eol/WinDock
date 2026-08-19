using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using WinDock.Models;

namespace WinDock.Services
{
    public sealed class DockStorageService
    {
        private const int CurrentSchemaVersion = 6;
        private readonly string _storePath;

        public DockStorageService()
            : this(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "WinDock",
                "dock-items.json"))
        {
        }

        public DockStorageService(string storePath)
        {
            if (string.IsNullOrWhiteSpace(storePath))
            {
                throw new ArgumentException("存储路径不能为空。", nameof(storePath));
            }

            _storePath = storePath;
        }

        public string StorePath
        {
            get { return _storePath; }
        }

        public DockStore Load()
        {
            if (!File.Exists(_storePath))
            {
                return CreateEmptyStore();
            }

            try
            {
                using (var stream = File.OpenRead(_storePath))
                {
                    var serializer = new DataContractJsonSerializer(typeof(DockStore));
                    var store = serializer.ReadObject(stream) as DockStore;
                    return Normalize(store);
                }
            }
            catch (SerializationException)
            {
                return CreateEmptyStore();
            }
            catch (IOException)
            {
                return CreateEmptyStore();
            }
        }

        public void Save(DockStore store)
        {
            if (store == null)
            {
                throw new ArgumentNullException(nameof(store));
            }

            store.SchemaVersion = CurrentSchemaVersion;
            store.Items = store.Items ?? new System.Collections.Generic.List<DockItem>();
            store.KnownApplicationIds = store.KnownApplicationIds ?? new System.Collections.Generic.List<string>();

            var directory = Path.GetDirectoryName(_storePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var temporaryPath = _storePath + ".tmp";
            using (var stream = File.Create(temporaryPath))
            {
                var serializer = new DataContractJsonSerializer(typeof(DockStore));
                serializer.WriteObject(stream, store);
            }

            if (File.Exists(_storePath))
            {
                File.Replace(temporaryPath, _storePath, null);
            }
            else
            {
                File.Move(temporaryPath, _storePath);
            }
        }

        private static DockStore CreateEmptyStore()
        {
            return new DockStore
            {
                SchemaVersion = CurrentSchemaVersion,
                IsInitialized = false,
                LastScanUtc = DateTime.MinValue
            };
        }

        private static DockStore Normalize(DockStore store)
        {
            if (store == null)
            {
                return CreateEmptyStore();
            }

            if (store.SchemaVersion < 2)
            {
                foreach (var item in store.Items ?? new System.Collections.Generic.List<DockItem>())
                {
                    item.Group = item.Source == DockItemSource.Desktop
                        ? DockItemGroup.Default
                        : DockItemGroup.More;
                }
            }

            if (store.SchemaVersion < 3)
            {
                // v3 起新增外观设置；旧存档缺少这些字段（反序列化后为 0/null），统一补默认值。
                store.FontFamilyName = string.IsNullOrWhiteSpace(store.FontFamilyName) ? "Segoe UI" : store.FontFamilyName;
                store.IconOpacity = 1;
                store.WindowOpacity = 1;
            }

            if (store.SchemaVersion < 4)
            {
                // v4 起新增窗口阴影与列表虚拟化开关，默认开启。
                store.WindowShadow = true;
                store.UseVirtualization = true;
            }

            if (store.SchemaVersion < 5)
            {
                // v5 起新增排序方式，默认手动拖动顺序。
                store.SortMode = 0;
            }

            store.SchemaVersion = CurrentSchemaVersion;
            store.Items = store.Items ?? new System.Collections.Generic.List<DockItem>();
            store.KnownApplicationIds = store.KnownApplicationIds ?? new System.Collections.Generic.List<string>();
            if (store.IconSize < 24 || store.IconSize > 128)
            {
                store.IconSize = 64;
            }

            if (string.IsNullOrWhiteSpace(store.FontFamilyName))
            {
                store.FontFamilyName = "Segoe UI";
            }

            if (double.IsNaN(store.IconOpacity) || store.IconOpacity < 0 || store.IconOpacity > 1)
            {
                store.IconOpacity = 1;
            }

            if (double.IsNaN(store.WindowOpacity) || store.WindowOpacity < 0.3 || store.WindowOpacity > 1)
            {
                store.WindowOpacity = 1;
            }

            return store;
        }

    }
}

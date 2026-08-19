using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace WinDock.Models
{
    [DataContract]
    public sealed class DockStore
    {
        public DockStore()
        {
            Items = new List<DockItem>();
            KnownApplicationIds = new List<string>();
        }

        [DataMember(Order = 1)]
        public int SchemaVersion { get; set; } = 1;

        [DataMember(Order = 2)]
        public bool IsInitialized { get; set; }

        [DataMember(Order = 3)]
        public DateTime LastScanUtc { get; set; }

        [DataMember(Order = 4)]
        public List<DockItem> Items { get; set; }

        [DataMember(Order = 5)]
        public List<string> KnownApplicationIds { get; set; }

        [DataMember(Order = 6)]
        public double IconSize { get; set; } = 64;

        [DataMember(Order = 7)]
        public double WindowLeft { get; set; }

        [DataMember(Order = 8)]
        public double WindowTop { get; set; }

        [DataMember(Order = 9)]
        public double WindowWidth { get; set; }

        [DataMember(Order = 10)]
        public double WindowHeight { get; set; }

        [DataMember(Order = 11)]
        public string WindowState { get; set; } = "Normal";

        [DataMember(Order = 12)]
        public string FontFamilyName { get; set; } = "Segoe UI";

        [DataMember(Order = 13)]
        public double IconOpacity { get; set; } = 1;

        [DataMember(Order = 14)]
        public double WindowOpacity { get; set; } = 1;

        [DataMember(Order = 15)]
        public bool WindowShadow { get; set; } = true;

        [DataMember(Order = 16)]
        public bool UseVirtualization { get; set; } = true;

        [DataMember(Order = 17)]
        public int SortMode { get; set; }
    }
}

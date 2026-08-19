using System;
using System.ComponentModel;
using System.Runtime.Serialization;

namespace WinDock.Models
{
    public enum DockItemSource
    {
        Desktop,
        StartMenu,
        Taskbar,
        Manual
    }

    public enum DockItemGroup
    {
        Default,
        More,
        Hidden
    }

    public enum DockSortMode
    {
        Default = 0,    // 手动拖动顺序
        NameAsc = 1,    // 名称 A → Z
        NameDesc = 2,   // 名称 Z → A
        InstallDesc = 3, // 安装时间 新 → 旧
        InstallAsc = 4   // 安装时间 旧 → 新
    }

    [DataContract]
    public sealed class DockItem : INotifyPropertyChanged
    {
        [DataMember(Order = 1)]
        public string Id { get; set; }

        [DataMember(Order = 2)]
        public string DisplayName { get; set; }

        [DataMember(Order = 3)]
        public string TargetPath { get; set; }

        [DataMember(Order = 4)]
        public string Arguments { get; set; }

        [DataMember(Order = 5)]
        public string IconPath { get; set; }

        [DataMember(Order = 6)]
        public DockItemSource Source { get; set; }

        [DataMember(Order = 7)]
        public DateTime FirstSeenUtc { get; set; }

        [DataMember(Order = 8)]
        public DateTime LastSeenUtc { get; set; }

        [DataMember(Order = 9)]
        public bool IsNew { get; set; }

        [DataMember(Order = 10)]
        public bool IsUnavailable { get; set; }

        [DataMember(Order = 11)]
        public DockItemGroup Group { get; set; }

        private string _note;

        [DataMember(Order = 12)]
        public string Note
        {
            get { return _note; }
            set
            {
                if (!string.Equals(_note, value, StringComparison.Ordinal))
                {
                    _note = value;
                    OnPropertyChanged(nameof(Note));
                }
            }
        }

        /// <summary>手动拖动排序的序号（同组内比较）。</summary>
        [DataMember(Order = 13)]
        public double Order { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string propertyName)
        {
            var handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}

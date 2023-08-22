using IPA.Config.Stores.Attributes;
using IPA.Config.Stores.Converters;
using UnityEngine;

namespace BookmarkViewer
{
    public class Config
    {
        public static Config? Instance { get; set; }

        public virtual bool Enabled { get; set; } = true;
        public virtual bool SnapToBookmark { get; set; } = true;
        public virtual float BookmarkWidthSize { get; set; } = 0.8f;
    }
}
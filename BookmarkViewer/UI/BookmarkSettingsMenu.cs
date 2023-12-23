using System;
using System.ComponentModel;
using System.Threading.Tasks;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components.Settings;
using BeatSaberMarkupLanguage.GameplaySetup;
using BeatSaberMarkupLanguage.Settings;
using UnityEngine;
using Zenject;

namespace BookmarkViewer.UI
{
	public class BookmarkSettingsMenu : MonoBehaviour, IInitializable, INotifyPropertyChanged
	{
		public event PropertyChangedEventHandler PropertyChanged = null!;


        [UIValue("enabled")]
        public bool Enabled
        {
            get => Config.Instance.Enabled;
            set
            {
                Config.Instance.Enabled = value;
            }
        }

        [UIValue("snap")]
        public bool SnapToBookmark
        {
            get => Config.Instance.SnapToBookmark;
            set
            {
                Config.Instance.SnapToBookmark = value;
            }

        }           
        [UIValue("skew")]
        public bool UnskewBookmarks
        {
            get => Config.Instance.UnskewBookmarks;
            set
            {
                Config.Instance.UnskewBookmarks = value;
            }
        }        
        
        [UIValue("width")]
        public float BookmarkWidthSize
        {
            get => Config.Instance.BookmarkWidthSize;
            set
            {
                Config.Instance.BookmarkWidthSize = value;
            }
        }

        public void Initialize()
		{
            BSMLSettings.instance.AddSettingsMenu("BookmarkViewer", "BookmarkViewer.UI.Menu.bsml", this);
		}
	}
}

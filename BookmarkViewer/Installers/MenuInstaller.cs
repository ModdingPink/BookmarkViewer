using BookmarkViewer.UI;
using Zenject;

namespace BookmarkViewer.Installers
{
    class MenuInstaller : Installer
    {
        public override void InstallBindings()
        {
            Container.BindInterfacesTo<BookmarkSettingsMenu>().FromNewComponentOnRoot().AsSingle();
        }
    }
}

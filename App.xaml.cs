using Microsoft.Extensions.DependencyInjection;
using pdfreader.Data;
using Microsoft.Maui.Storage;
using pdfreader.Models;

namespace pdfreader
{
    public partial class App : Application
    {
        internal static LibraryDatabase Database { get; private set; }

        static App()
        {
            var path = Path.Combine(FileSystem.AppDataDirectory, "MyLibrary.db3");
            Database = new LibraryDatabase(path);
        }

        public App()
        {
            InitializeComponent();
            // Database is initialized in the static constructor
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new NavigationPage(new MainPage()));
        }
    }
}
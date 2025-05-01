using System;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Media.Imaging;

using Autodesk.Revit.UI;

namespace LintelsOnTheFloor
{
    public partial class App : Application
    {
        private static App _instance;
        public static ExternalCommandData CommandData { get; private set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            _instance = this;
            base.OnStartup(e);
        }

        public static void Initialize(ExternalCommandData commandData)
        {
            if (_instance == null)
            {
                new App();
            }
            CommandData = commandData;
        }
    }
}

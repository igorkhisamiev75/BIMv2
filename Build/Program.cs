using Microsoft.Deployment.WindowsInstaller;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WixSharp;
using static WixSharp.SetupEventArgs;
using static WixSharp.Win32;

namespace Build
{
    internal class Program
    {
        private static string projectName = "BIMv2Buttons";
        private static string version = "1.0.1";
        static void Main(string[] args)
        {
            var project = new Project()
            {
                Name = projectName,
                UI = WUI.WixUI_ProgressOnly,
                OutDir = "output",
                GUID = new Guid("034B8E0C-63C0-4761-AB10-72314D4C2744"),
                MajorUpgrade = MajorUpgrade.Default,
                ControlPanelInfo =
                {
                    Manufacturer = Environment.UserName,
                },
                Dirs = new Dir[]
                {
                    new InstallDir(@"%AppDataFolder%\Autodesk\Revit\Addins\2021\",
                        new File(@"C:\Users\i.khisamiev\source\repos\BIMv2\BIMv2Buttons\BIMv2Buttons.addin"),
                        new Dir(@"BIMv2Buttons",
                        new DirFiles(@"C:\Users\i.khisamiev\source\repos\BIMv2\BIMv2Buttons\bin\R2021\*.*")))
                },

            };
            project.Version = new Version(version);

            project.BuildMsi();
        }
    }
}

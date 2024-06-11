using Autodesk.Revit.DB;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RenameSheetsWPF
{
    public class SheetInProject
    {
        public string stadiy { get; set; }
        public string albom { get; set; }
        public string numberSheet { get; set; }
        public string nameSheet { get; set; }
        public ElementId idSheet { get; set; }

        public BrowserOrganization bOrg { get; }

        public ElementId id { get; set; }

    }
}

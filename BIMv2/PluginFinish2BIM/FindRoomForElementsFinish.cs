using System;
using System.Collections.Generic;
using System.Linq;

using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;

namespace BIMv2
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class FindRoomForElementsFinish : IExternalCommand
    {
        public List<Railing> AllFinishWall = new List<Railing>();

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            Document doc = commandData.Application.ActiveUIDocument.Document;

            using (var t = new Transaction(doc))
            {
                t.Start("Ищем помещения  для отделочных элементов");

                //записываем помещения
                WriteToWallSquare(doc);

                

                t.Commit();

            }
            return Result.Succeeded;

        }
        public void WriteToWallSquare(Document doc)
        {
            //получить все ограждения
            var allRailings = GetAllRailings(doc);

            //получить все стены
            var allWalList = GetAllWalls(doc);



            AllFinishWall.Clear();

            foreach (var element in allRailings)
            {
                try
                {
                    var wall = (Railing)element;

                    var typeGetCeiling = doc.GetElement(wall.GetTypeId());
                    var descriptionWallType = typeGetCeiling.LookupParameter("Описание").AsString();

                    if (descriptionWallType == "Отделка")
                    {
                        AllFinishWall.Add(wall);
                    }

                }
                catch (Exception e)
                {
                    continue;
                }


            }

            foreach (var railing in AllFinishWall)
            {
                string nameRoom = "";

                Phase phaseCreated = railing.Document.GetElement(railing.CreatedPhaseId) as Phase;
                
                var pointForSearch = railing.get_BoundingBox(doc.ActiveView).Max;

                var r = doc.GetRoomAtPoint(pointForSearch, phaseCreated);

                var myRoom = FindRoom(doc, pointForSearch, phaseCreated);

                if (r != null)
                {
                    nameRoom = r.get_Parameter(BuiltInParameter.ROOM_NUMBER).AsString();

                    //ADSK_Примечание
                    railing.get_Parameter(new Guid("a85b7661-26b0-412f-979c-66af80b4b2c3")).Set(nameRoom);
                  
                }


            }


        }

        public IList<Element> GetAllRailings(Document document)
        {
            // List all Floor's
            var floor_collector = new FilteredElementCollector(document)
                .OfCategory(BuiltInCategory.OST_StairsRailing)
                .WhereElementIsNotElementType();

            IList<Element> allFloors = floor_collector.ToElements();
            return allFloors;
        }

        public IList<Element> GetAllWalls(Document document)
        {
            // List all Wall's
            var floor_collector = new FilteredElementCollector(document)
                .OfCategory(BuiltInCategory.OST_Walls)
                .WhereElementIsNotElementType();

            IList<Element> allFloors = floor_collector.ToElements();
            return allFloors;
        }

        public Room FindRoom(Document doc, XYZ newSearchPoint, Phase phaseCreated)
        {

            if (doc != null)
            {
                return doc
                    .Phases
                    .Cast<Phase>()
                    .Select(x => doc.GetRoomAtPoint(newSearchPoint, x))
                    .FirstOrDefault(x => x != null);
            }

            return null;
        }

    }
}

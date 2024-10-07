using System;
using System.Collections.Generic;
using System.Linq;

using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;


namespace BIMv2
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]


    public class GetPerimeterForFloorWithoutDoor : IExternalCommand
    {
        public List<Floor> AllF = new List<Floor>();
        string ADSK_Примечание = "a85b7661-26b0-412f-979c-66af80b4b2c3";
     
        string BIM_BOP_ПериметрСВычетомДверныхПроемов = "a0f435e9-380d-4734-905c-9f96ca39ca87";
        string BIM_BOP_ПериметрСВычетомДверныхПроемовЧисловой = "3e207c92-bd4d-4876-b309-2b64c77ef7f9";


        private Document _doc;


        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uiApp = commandData.Application;
            var uiDoc = uiApp.ActiveUIDocument;

            _doc = uiDoc.Document;

            Document doc = commandData.Application.ActiveUIDocument.Document;

          

            using (var t = new Transaction(doc))
            {
                t.Start("Stage 2");
                Document documentLinkModelAr = doc;
                //получить все двери со связей

                IList<Element> doorList= new List<Element>();

                IEnumerable<RevitLinkInstance> linkedInstances = FindLinkedInstances();

                //находим AR

                foreach (var linkedInstance in linkedInstances)
                {
                    Document d = linkedInstance.GetLinkDocument();


                    if (d!=null &&  d.Title == "27_PD_AR_ALL_MRGT_R23_SHD")
                    {
                        doorList = GetAllDoorElements(d);
                        documentLinkModelAr = d;
                    }

                }

                //TaskDialog.Show("Сделано", $"Дверей в проекте АР {doorList.Count}");


                WriteToComment(doc, doorList, documentLinkModelAr);

                //TaskDialog.Show("Сделано", "Периметры прописаны в перекрытия с вычетом дверных проемов для ВОР");

                t.Commit();

            }

            return Result.Succeeded;

        }

        public void WriteToComment(Document doc, IList<Element> elementsDoor, Document d2)
        {
            IList<Element> allFloors = GetAllFloorElements(doc);

            foreach (Element floorElement in allFloors)
            {
                try
                {

                    //периметр перекрытия
                    string perimetrStringFloor = floorElement.get_Parameter(BuiltInParameter.HOST_PERIMETER_COMPUTED).AsValueString();

                    double perimetrInt = Convert.ToDouble(perimetrStringFloor);

                    //номер помещения записанный в перекрытии
                    string namberRoomFloor = floorElement.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS).AsValueString()
                        +" "+ floorElement.get_Parameter(new Guid(ADSK_Примечание)).AsString();

                    //получим уровень

                    var floorLevel=floorElement.get_Parameter(BuiltInParameter.SCHEDULE_LEVEL_PARAM).AsValueString();


                    //проходим по дверям
                    foreach (Element doorElement in elementsDoor)
                    {
                        FamilyInstance familyInstance = doorElement as FamilyInstance;

                        ElementType type = d2.GetElement(doorElement.GetTypeId()) as ElementType;
                        string widhtDoor = "0";
                        string levelDoor = doorElement.get_Parameter(BuiltInParameter.SCHEDULE_LEVEL_PARAM).AsValueString();

                        //ширина двери в типе
                        if (type != null)
                        {
                            widhtDoor = type.get_Parameter(BuiltInParameter.GENERIC_WIDTH).AsValueString();
                           
                        }


                        double widhtDoorInt = Convert.ToDouble(widhtDoor);


                        if (familyInstance != null && 
                            familyInstance.Name != "ADSK_Дверь_Проем"&&
                            floorLevel==levelDoor
                            )
                        {

                            try
                            {
                                string numberDoorToRoom = familyInstance.ToRoom.Name;
                                string numberDoorFromRoom = familyInstance.FromRoom.Name;

                                if (namberRoomFloor == numberDoorToRoom )
                                {
                                    //TaskDialog.Show("Сделано", $"Зашли сюда numberDoorToRoom {numberDoorToRoom}");
                                    //TaskDialog.Show("Сделано", $"Зашли сюда дверь numberDoorToRoom {doorElement.Name}");

                                    perimetrInt = perimetrInt - widhtDoorInt;
                                }


                                if (namberRoomFloor == numberDoorFromRoom)
                                {
                                    //TaskDialog.Show("Сделано", $"Дверей в проекте АР numberDoorFromRoom {numberDoorFromRoom}");
                                    //TaskDialog.Show("Сделано", $"Зашли сюда дверь numberDoorToRoom {doorElement.Name}");

                                    perimetrInt = perimetrInt - widhtDoorInt;
                                }

                            }
                            catch { }

                        }

                    }

                    floorElement.get_Parameter(new Guid(BIM_BOP_ПериметрСВычетомДверныхПроемов)).Set(perimetrInt.ToString());

                    double perDouble=Convert.ToDouble(perimetrInt.ToString());
                    double perimetrInt2 = UnitUtils.ConvertToInternalUnits(perDouble, UnitTypeId.Millimeters);

                  

                    floorElement.get_Parameter(new Guid(BIM_BOP_ПериметрСВычетомДверныхПроемовЧисловой)).Set(perimetrInt2);

                }
                catch (Exception e)

                {
                    //TaskDialog.Show("Сделано", $"{e.Message}");
                    floorElement.get_Parameter(new Guid(BIM_BOP_ПериметрСВычетомДверныхПроемов)).Set("Error");
                }
             }

        }

        public IList<Element> GetAllFloorElements(Document document)
        {
            // List all Floor's
            var floor_collector = new FilteredElementCollector(document)
                .OfCategory(BuiltInCategory.OST_Floors)
                .WhereElementIsNotElementType();
            IList<Element> allFloors = floor_collector.ToElements();

            return allFloors;
        }


        public string GetSquareFloorByNameRoom(string name, List<Floor> floors)
        {

            IEnumerable<Floor> gg = floors.Where(x => x.get_Parameter(new Guid(ADSK_Примечание))
          .AsString() == name);

            //gg.get_Parameter(BuiltInParameter.HOST_AREA_COMPUTED).AsValueString();
            if (gg.Count() != 0)
            {
                Floor f = gg.FirstOrDefault();

                return f.get_Parameter(BuiltInParameter.HOST_AREA_COMPUTED).AsValueString();
                //return square;
            }

            else { return string.Empty; }



        }

        private IEnumerable<RevitLinkInstance> FindLinkedInstances()
        {
            var collector = new FilteredElementCollector(_doc);
            return collector
                .OfClass(typeof(RevitLinkInstance))
                .Cast<RevitLinkInstance>()
                .ToList();
        }
        public IList<Element> GetAllDoorElements(Document document)
        {
            // List all Door's
            var collector = new FilteredElementCollector(document)
                .OfCategory(BuiltInCategory.OST_Doors)
                .WhereElementIsNotElementType();

            IList<Element> elements = collector.ToElements();
            return elements;
        }
    }
}

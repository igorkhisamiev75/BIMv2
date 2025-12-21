using System;
using System.Collections;
using System.Collections.Generic;

using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

using Application = Autodesk.Revit.ApplicationServices.Application;


namespace BIMv2
{
    [Transaction(TransactionMode.Manual)]
    class DoorsInformationTransferBim : IExternalCommand
    {
        private Application _app;
        private Document _doc;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uiApp = commandData.Application;
            var uiDoc = uiApp.ActiveUIDocument;

            _app = uiApp.Application;
            _doc = uiDoc.Document;

            using (var t = new Transaction(_doc))
            {
                t.Start("Run");
                uiApp = commandData.Application;
                uiDoc = uiApp.ActiveUIDocument;

                var allDoors = GetAllDoorElements(_doc);
                var allMaterials = GetAllMaterials(_doc);

                foreach (Element door in allDoors)
                {
                    try
                    {
                        var bimOtdelkaDvery = door.LookupParameter("BIM_Отделка двери").AsElementId();
                        var bimColorFurnit = door.LookupParameter("BIM_Цвет фурнитуры").AsElementId();
                        var bimComplictazia = door.LookupParameter("BIM_Комплектация").AsElementId();

                        //берем параметры BIM_ОтделкаДвери
                        //полотно
                        var BIM_PolotnVnutrColor = _doc.GetElement(bimOtdelkaDvery).LookupParameter("BIM_ПолотноВ_Цвет").AsString();
                        var BIM_PolotnVnutrMat = _doc.GetElement(bimOtdelkaDvery).LookupParameter("BIM_ПолотноВ_Материал").AsElementId();
                        var BIM_PolotnNarColor = _doc.GetElement(bimOtdelkaDvery).LookupParameter("BIM_ПолотноН_Цвет").AsString();
                        var BIM_PolotnNarMat = _doc.GetElement(bimOtdelkaDvery).LookupParameter("BIM_ПолотноН_Материал").AsElementId();

                        door.LookupParameter("BIM_ПолотноН_Цвет").Set(BIM_PolotnNarColor);
                        door.LookupParameter("BIM_ПолотноН_Материал").Set(BIM_PolotnNarMat);
                        door.LookupParameter("BIM_ПолотноВ_Цвет").Set(BIM_PolotnVnutrColor);
                        door.LookupParameter("BIM_ПолотноВ_Материал").Set(BIM_PolotnVnutrMat);

                        //наличники и доборы
                        //наружний
                        var BIM_NalishnikNalNar = _doc.GetElement(bimOtdelkaDvery).LookupParameter("BIM_НаличникН_Наличие").AsInteger();
                        var BIM_NalishnikShirNar = _doc.GetElement(bimOtdelkaDvery).LookupParameter("BIM_НаличникН_Ширина").AsDouble();
                        var BIM_NalishnikNarMat = _doc.GetElement(bimOtdelkaDvery).LookupParameter("BIM_НаличникН_Материал").AsElementId();
                        var BIM_NalishnikNarColor = _doc.GetElement(bimOtdelkaDvery).LookupParameter("BIM_НаличникН_Цвет").AsString();

                        door.LookupParameter("BIM_НаличникН_Наличие").Set(BIM_NalishnikNalNar);
                        door.LookupParameter("BIM_НаличникН_Ширина").Set(BIM_NalishnikShirNar);
                        door.LookupParameter("BIM_НаличникН_Материал").Set(BIM_NalishnikNarMat);
                        door.LookupParameter("BIM_НаличникН_Цвет").Set(BIM_NalishnikNarColor);



                        //внутренний
                        var BIM_NalishnikNalVntr = _doc.GetElement(bimOtdelkaDvery).LookupParameter("BIM_НаличникВ_Наличие").AsInteger();
                        var BIM_NalishnikShirVntr = _doc.GetElement(bimOtdelkaDvery).LookupParameter("BIM_НаличникВ_Ширина").AsDouble();
                        var BIM_NalishnikVntrMat = _doc.GetElement(bimOtdelkaDvery).LookupParameter("BIM_НаличникВ_Материал").AsElementId();
                        var BIM_NalishnikVntrColor = _doc.GetElement(bimOtdelkaDvery).LookupParameter("BIM_НаличникВ_Цвет").AsString();

                        door.LookupParameter("BIM_НаличникВ_Наличие").Set(BIM_NalishnikNalVntr);
                        door.LookupParameter("BIM_НаличникВ_Ширина").Set(BIM_NalishnikShirVntr);
                        door.LookupParameter("BIM_НаличникВ_Материал").Set(BIM_NalishnikVntrMat);
                        door.LookupParameter("BIM_НаличникВ_Цвет").Set(BIM_NalishnikVntrColor);

                        //добор

                        var BIM_DoborNal = _doc.GetElement(bimOtdelkaDvery).LookupParameter("BIM_Добор_Наличие").AsInteger();
                        var BIM_DoborColor = _doc.GetElement(bimOtdelkaDvery).LookupParameter("BIM_Добор_Цвет").AsString();
                        door.LookupParameter("BIM_Добор_Наличие").Set(BIM_DoborNal);
                        door.LookupParameter("BIM_Добор_Цвет").Set(BIM_DoborColor);

                        //фальшпанель
                        //наружний
                        var BIM_FalshNalNar = _doc.GetElement(bimOtdelkaDvery).LookupParameter("BIM_ФальшпанельН_Наличие").AsInteger();
                        var BIM_FalshShirNar = _doc.GetElement(bimOtdelkaDvery).LookupParameter("BIM_ФальшпанельН_Высота").AsDouble();
                        var BIM_FalshNarMat = _doc.GetElement(bimOtdelkaDvery).LookupParameter("BIM_ФальшпанельН_Материал").AsElementId();
                        var BIM_FalshNarColor = _doc.GetElement(bimOtdelkaDvery).LookupParameter("BIM_ФальшпанельН_Цвет").AsString();

                        door.LookupParameter("BIM_ФальшпанельН_Наличие").Set(BIM_FalshNalNar);
                        door.LookupParameter("BIM_ФальшпанельН_Высота").Set(BIM_FalshShirNar);
                        door.LookupParameter("BIM_ФальшпанельН_Материал").Set(BIM_FalshNarMat);
                        door.LookupParameter("BIM_ФальшпанельН_Цвет").Set(BIM_FalshNarColor);


                        //внутренний
                        var BIM_FalshNalVntr = _doc.GetElement(bimOtdelkaDvery).LookupParameter("BIM_ФальшпанельВ_Наличие").AsInteger();
                        var BIM_FalshShirVntr = _doc.GetElement(bimOtdelkaDvery).LookupParameter("BIM_ФальшпанельВ_Высота").AsDouble();
                        var BIM_FalshVntrMat = _doc.GetElement(bimOtdelkaDvery).LookupParameter("BIM_ФальшпанельВ_Материал").AsElementId();
                        var BIM_FalshVntrColor = _doc.GetElement(bimOtdelkaDvery).LookupParameter("BIM_ФальшпанельВ_Цвет").AsString();

                        door.LookupParameter("BIM_ФальшпанельВ_Наличие").Set(BIM_FalshNalVntr);
                        door.LookupParameter("BIM_ФальшпанельВ_Высота").Set(BIM_FalshShirVntr);
                        door.LookupParameter("BIM_ФальшпанельВ_Материал").Set(BIM_FalshVntrMat);
                        door.LookupParameter("BIM_ФальшпанельВ_Цвет").Set(BIM_FalshVntrColor);

                        //фурнитура

                        var BIM_DovodColor = _doc.GetElement(bimColorFurnit).LookupParameter("BIM_Доводчик_Цвет").AsString();
                        var BIM_PetlColor = _doc.GetElement(bimColorFurnit).LookupParameter("BIM_Петли_Цвет").AsString();
                        var BIM_RuchkiColor = _doc.GetElement(bimColorFurnit).LookupParameter("BIM_Ручки_Цвет").AsString();
                        var BIM_RuchkiMat = _doc.GetElement(bimColorFurnit).LookupParameter("BIM_Ручки_Материал").AsElementId();
                        var BIM_ZamokColor = _doc.GetElement(bimColorFurnit).LookupParameter("BIM_Замок_Цвет").AsString();
                        var BIM_PorogColor = _doc.GetElement(bimColorFurnit).LookupParameter("BIM_Порог_Цвет").AsString();

                        door.LookupParameter("BIM_Доводчик_Цвет").Set(BIM_DovodColor);
                        door.LookupParameter("BIM_Петли_Цвет").Set(BIM_PetlColor);
                        door.LookupParameter("BIM_Ручки_Цвет").Set(BIM_RuchkiColor);
                        door.LookupParameter("BIM_Ручки_Материал").Set(BIM_RuchkiMat);
                        door.LookupParameter("BIM_Замок_Цвет").Set(BIM_ZamokColor);
                        door.LookupParameter("BIM_Порог_Цвет").Set(BIM_PorogColor);

                        var BIM_Dovod = _doc.GetElement(bimComplictazia).LookupParameter("BIM_Доводчик").AsString();
                        var BIM_Petl = _doc.GetElement(bimComplictazia).LookupParameter("BIM_Петли").AsString();
                        var BIM_Porog = _doc.GetElement(bimComplictazia).LookupParameter("BIM_Порог").AsString();
                        var BIM_Ruchki = _doc.GetElement(bimComplictazia).LookupParameter("BIM_Ручки").AsString();
                        var BIM_Zamok = _doc.GetElement(bimComplictazia).LookupParameter("BIM_Замок").AsString();
                        var BIM_Skud = _doc.GetElement(bimComplictazia).LookupParameter("BIM_СКУД").AsString();
                        var BIM_SOS = _doc.GetElement(bimComplictazia).LookupParameter("BIM_СОС").AsString();
                        var BIM_Otboinik = _doc.GetElement(bimComplictazia).LookupParameter("BIM_Отбойник").AsString();

                        door.LookupParameter("BIM_Доводчик").Set(BIM_Dovod);
                        door.LookupParameter("BIM_Петли").Set(BIM_Petl);
                        door.LookupParameter("BIM_Порог").Set(BIM_Porog);
                        door.LookupParameter("BIM_Ручки").Set(BIM_Ruchki);
                        door.LookupParameter("BIM_Замок").Set(BIM_Zamok);
                        door.LookupParameter("BIM_СКУД").Set(BIM_Skud);
                        door.LookupParameter("BIM_СОС").Set(BIM_SOS);
                        door.LookupParameter("BIM_Отбойник").Set(BIM_Otboinik);


                    }

                    catch { }





                }

                TaskDialog.Show("Сделано!", $"Информация перенесена для {allDoors.Count} дверей");

                t.Commit();

            }


            return Result.Succeeded;
        }

        public string GetDescriptionInMaterial(string nameMaterial)
        {
            var allMaterials = GetAllMaterials(_doc);
            string opisanie = "";

            foreach (var material in allMaterials)
            {
                if (material.Name == nameMaterial)
                {
                    opisanie = material.LookupParameter("Описание").AsString();
                }
            }

            return opisanie;
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

        public IList<Element> GetAllMaterials(Document document)
        {
            // List all Door's
            var collector = new FilteredElementCollector(document)
                .OfCategory(BuiltInCategory.OST_Materials)
                .WhereElementIsNotElementType();

            IList<Element> elements = collector.ToElements();
            return elements;
        }


    }

}

using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

using System;
using System.Linq;

namespace CurtainPanelDimensions
{
    [Transaction(TransactionMode.Manual)]
    public class GetTrimmedPanelSize : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;

            // 1. Получаем выделенный элемент
            Selection sel = uidoc.Selection;
            if (sel.GetElementIds().Count != 1)
            {
                TaskDialog.Show("Ошибка", "Выберите одну панель витража");
                return Result.Cancelled;
            }

            ElementId panelId = sel.GetElementIds().First();
            Element panel = doc.GetElement(panelId);

            // Проверка: является ли элемент панелью витража
            if (!IsCurtainWallPanel(panel))
            {
                TaskDialog.Show("Ошибка", "Выбранный элемент не является панелью витража");
                return Result.Failed;
            }

            // 2. Получаем геометрию и вычисляем фактические размеры
            double actualWidth = 0;
            double actualHeight = 0;

            if (!TryGetTrimmedPanelDimensions(doc, panel, out actualWidth, out actualHeight))
            {
                TaskDialog.Show("Ошибка", "Не удалось вычислить размеры панели");
                return Result.Failed;
            }

            // 3. Формируем строку для записи в ADSK_Примечание
            double widthMM = UnitUtils.ConvertFromInternalUnits(actualWidth, UnitTypeId.Millimeters);
            double heightMM = UnitUtils.ConvertFromInternalUnits(actualHeight, UnitTypeId.Millimeters);

            string note = $"Ширина: {Math.Round(widthMM, 0)} мм, Высота: {Math.Round(heightMM, 0)} мм (с учетом подрезки)";

            // 4. Записываем в параметр ADSK_Примечание
            using (Transaction trans = new Transaction(doc, "Запись размеров панели"))
            {
                trans.Start();

                Parameter adskNote = panel.LookupParameter("ADSK_Примечание");

                if (adskNote == null)
                {
                    TaskDialog.Show("Ошибка", "Параметр 'ADSK_Примечание' не найден");
                    return Result.Failed;
                }

                if (!adskNote.IsReadOnly)
                {
                    adskNote.Set(note);
                }
                else
                {
                    TaskDialog.Show("Ошибка", "Параметр 'ADSK_Примечание' доступен только для чтения");
                    return Result.Failed;
                }

                trans.Commit();
            }

            TaskDialog.Show("Успешно",
                $"Размеры панели записаны в ADSK_Примечание:\n{note}\n\n" +
                $"Ширина (футы): {actualWidth:F4}\nВысота (футы): {actualHeight:F4}");

            return Result.Succeeded;
        }

        /// <summary>
        /// Проверка, является ли элемент панелью витража
        /// </summary>
        private bool IsCurtainWallPanel(Element element)
        {
            // Панели витража могут быть как встроенными (Panel), так и загружаемыми семействами
            return element is Panel ||
                   element.Category?.Id.IntegerValue.Equals((int)BuiltInCategory.OST_CurtainWallPanels) == true;
        }

        /// <summary>
        /// Получение фактических размеров панели с учетом подрезки
        /// </summary>
        private bool TryGetTrimmedPanelDimensions(Document doc, Element panel, out double width, out double height)
        {
            width = 0;
            height = 0;

            try
            {
                Options geoOptions = new Options();
                geoOptions.ComputeReferences = false;
                geoOptions.DetailLevel = ViewDetailLevel.Fine;
                geoOptions.IncludeNonVisibleObjects = false;

                GeometryElement geoElement = panel.get_Geometry(geoOptions);

                if (geoElement == null) return false;

                Solid targetSolid = null;

                // Ищем Solid в геометрии
                foreach (GeometryObject geoObj in geoElement)
                {
                    if (geoObj is Solid solid && solid.Faces.Size > 0 && solid.Volume > 0)
                    {
                        targetSolid = solid;
                        break;
                    }
                    else if (geoObj is GeometryInstance instance)
                    {
                        GeometryElement instanceGeo = instance.GetInstanceGeometry();
                        foreach (GeometryObject instObj in instanceGeo)
                        {
                            if (instObj is Solid instSolid && instSolid.Faces.Size > 0 && instSolid.Volume > 0)
                            {
                                targetSolid = instSolid;
                                break;
                            }
                        }
                    }
                    if (targetSolid != null) break;
                }

                if (targetSolid == null) return false;

                // Получаем ограничивающий бокс с учетом трансформации
                BoundingBoxXYZ bbox = targetSolid.GetBoundingBox();
                Transform transform = bbox.Transform;

                XYZ min = transform.OfPoint(bbox.Min);
                XYZ max = transform.OfPoint(bbox.Max);

                // Для панелей витража обычно:
                // X - ширина (вдоль витража)
                // Z - высота
                // Y - толщина панели
                width = Math.Abs(max.X - min.X);
                height = Math.Abs(max.Z - min.Z);

                return width > 0 && height > 0;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Ошибка геометрии", ex.Message);
                return false;
            }
        }
    }
}
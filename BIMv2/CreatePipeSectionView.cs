using System;
using System.Linq;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Autodesk.Revit.ApplicationServices;

namespace BIMv2
{

    [Transaction(TransactionMode.Manual)]

    public class CreatePipeSectionView : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;
            UIDocument uiDoc = uiApp.ActiveUIDocument;
            Document doc = uiDoc.Document;

            try
            {
                // Выбор трубы
                Reference pipeRef = uiDoc.Selection.PickObject(ObjectType.Element, new PipeSelectionFilter(), "Выберите трубу");
                Pipe pipe = doc.GetElement(pipeRef) as Pipe;

                if (pipe == null)
                {
                    message = "Выбранный элемент не является трубой.";
                    return Result.Failed;
                }

                // Получаем геометрию трубы
                LocationCurve pipeCurve = pipe.Location as LocationCurve;
                Line pipeLine = pipeCurve?.Curve as Line;

                if (pipeLine == null)
                {
                    message = "Труба должна быть прямой для создания разреза.";
                    return Result.Failed;
                }

                // Вычисляем параметры вида
                XYZ pipeDirection = pipeLine.Direction;
                XYZ viewDirection = pipeDirection.CrossProduct(XYZ.BasisZ).Normalize();
                if (viewDirection.IsZeroLength())
                    viewDirection = XYZ.BasisX; // Запасной вариант

                XYZ upDirection = XYZ.BasisZ;
                XYZ origin = pipeLine.Evaluate(0.5, true); // Центр трубы

                // Создаем ограничивающий ящик
                BoundingBoxXYZ pipeBBox = pipe.get_BoundingBox(null);
                double offset = 2.0; // Отступ в футах

                using (Transaction trans = new Transaction(doc, "Create Pipe Section"))
                {
                    trans.Start();

                    // Создаем вид разреза
                    BoundingBoxXYZ sectionBox = new BoundingBoxXYZ();
                    sectionBox.Min = new XYZ(pipeBBox.Min.X - offset, pipeBBox.Min.Y - offset, pipeBBox.Min.Z - offset);
                    sectionBox.Max = new XYZ(pipeBBox.Max.X + offset, pipeBBox.Max.Y + offset, pipeBBox.Max.Z + offset);

                    // Устанавливаем ориентацию через преобразование
                    Transform transform = Transform.Identity;
                    transform.Origin = origin;
                    transform.BasisX = -viewDirection; // Ось X вида (направление взгляда)
                    transform.BasisY = upDirection;     // Ось Y вида (верх)
                    transform.BasisZ = transform.BasisX.CrossProduct(transform.BasisY); // Ось Z вида

                    sectionBox.Transform = transform;

                    ViewSection sectionView = ViewSection.CreateSection(doc,
                        doc.GetDefaultElementTypeId(ElementTypeGroup.ViewTypeSection),
                        sectionBox);

                    sectionView.Name = $"Разрез трубы {pipe.Name}";

                    trans.Commit();

                    // Активируем созданный вид
                    uiDoc.ActiveView = sectionView;
                }

                return Result.Succeeded;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }
    }

    public class PipeSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem) => elem is Pipe;
        public bool AllowReference(Reference reference, XYZ position) => false;
    }
}

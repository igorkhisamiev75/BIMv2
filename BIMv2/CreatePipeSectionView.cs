using System;
using System.Linq;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;

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

                //// Вычисляем параметры вида
                //XYZ pipeDirection = pipeLine.Direction;
                //XYZ viewDirection = pipeDirection.CrossProduct(XYZ.BasisZ).Normalize();
                //if (viewDirection.IsZeroLength())
                //    viewDirection = XYZ.BasisX; // Запасной вариант

                //XYZ upDirection = XYZ.BasisZ;
                //XYZ origin = pipeLine.Evaluate(0.5, true); // Центр трубы

                //// Создаем ограничивающий ящик
                //BoundingBoxXYZ pipeBBox = pipe.get_BoundingBox(null);
                //double offset = 2.0; // Отступ в футах

                using (Transaction trans = new Transaction(doc, "Create Pipe Section"))
                {
                    trans.Start();

                    //// Создаем вид разреза


                    BoundingBoxXYZ sectionBox2 = GetSectionViewParallelPipes(pipe);

                    ViewSection sectionView = ViewSection.CreateSection(doc,
                        doc.GetDefaultElementTypeId(ElementTypeGroup.ViewTypeSection),
                        sectionBox2);

                    sectionView.Name = $"Разрез трубы {pipe.Id}";

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
                //TaskDialog.Show("1", ex.Message.ToString());
                message = ex.Message;
                return Result.Failed;
            }
        }

        BoundingBoxXYZ GetSectionViewParallelPipes(Pipe pipe)
        {
            if (!(pipe.Location is LocationCurve lc))
                throw new ArgumentException("Элемент не имеет LocationCurve.");

            Curve curve = lc.Curve;
            XYZ pipeDirection = (curve.GetEndPoint(1) - curve.GetEndPoint(0)).Normalize();
            XYZ midpoint = curve.Evaluate(0.5, true);

            // Получаем габариты трубы
            BoundingBoxXYZ bb = pipe.get_BoundingBox(null);
            double length = curve.Length;
            double height = bb.Max.Z - bb.Min.Z;
            double diameter = pipe.Diameter;
            double offset = diameter * 2; // Отступ для видимости

            // Определяем направление вида и "вверх"
            XYZ viewDirection, upDirection;

            // Если труба вертикальная (параллельна оси Z)
            if (Math.Abs(pipeDirection.Z) > 0.99)
            {
                viewDirection = XYZ.BasisY; // Вид сбоку
                upDirection = XYZ.BasisZ;
            }
            // Если труба почти горизонтальная (перпендикулярна оси Z)
            else if (Math.Abs(pipeDirection.Z) < 0.1)
            {
                viewDirection = pipeDirection.CrossProduct(XYZ.BasisZ).Normalize();
                upDirection = XYZ.BasisZ;
            }
            // Если труба наклонная
            else
            {
                // Находим горизонтальную перпендикулярную ось
                XYZ horizontalPerpendicular = new XYZ(-pipeDirection.Y, pipeDirection.X, 0).Normalize();
                viewDirection = horizontalPerpendicular.CrossProduct(pipeDirection).Normalize();
                upDirection = pipeDirection.CrossProduct(viewDirection).Normalize();
            }

            // Проверяем, что направления корректны
            if (viewDirection.IsZeroLength() || upDirection.IsZeroLength())
                throw new InvalidOperationException("Не удалось вычислить направления вида.");

            // Настраиваем BoundingBox
            XYZ min = new XYZ(-length / 2 - offset, -offset, -offset);
            XYZ max = new XYZ(length / 2 + offset, height + offset, offset);

            // Задаем трансформацию
            Transform transform = Transform.Identity;
            transform.Origin = midpoint;
            transform.BasisX = pipeDirection;
            transform.BasisY = upDirection;
            transform.BasisZ = viewDirection;

            XYZ cross = transform.BasisX.CrossProduct(transform.BasisY);
            if (!cross.IsAlmostEqualTo(transform.BasisZ))
            {
                transform.BasisX = -transform.BasisX;
            }

            BoundingBoxXYZ n = new BoundingBoxXYZ();
            n.Max = max;
            n.Min = min;           
            n.Transform = transform;
            return n;
            

            return new BoundingBoxXYZ
            {
                Transform = transform,
                Min = min,
                Max = max
            };
        }
    }



    public class PipeSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem) => elem is Pipe;
        public bool AllowReference(Reference reference, XYZ position) => false;
    }


}

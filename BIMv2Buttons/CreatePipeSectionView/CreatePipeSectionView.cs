using System;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
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
                Reference pipeRef = uiDoc.Selection.PickObject(ObjectType.Element,
                    new PipeSelectionFilter(), "Выберите трубу");
                Pipe pipe = doc.GetElement(pipeRef) as Pipe;

                if (pipe == null)
                {
                    message = "Выбранный элемент не является трубой.";
                    return Result.Failed;
                }

                using (Transaction trans = new Transaction(doc, "Create Pipe Section"))
                {
                    trans.Start();

                    // Получаем параметры трубы
                    LocationCurve lc = pipe.Location as LocationCurve;
                    Curve curve = lc.Curve;
                    XYZ pipeDirection = (curve.GetEndPoint(1) - curve.GetEndPoint(0)).Normalize();
                    XYZ midpoint = curve.Evaluate(0.5, true);

                    // Определяем размеры области разреза
                    double diameter = pipe.Diameter;
                    double length = curve.Length;
                    double offset = diameter * 3; // Отступ вокруг трубы

                    // Создаем ограничивающий ящик
                    BoundingBoxXYZ sectionBox = new BoundingBoxXYZ();
                    sectionBox.Min = new XYZ(-length / 2 - offset, -offset, -offset);
                    sectionBox.Max = new XYZ(length / 2 + offset, offset, offset);

                    // Определяем ориентацию вида
                    XYZ viewDirection, upDirection, rightDirection;

                    // Для вертикальных труб
                    if (Math.Abs(pipeDirection.Z) > 0.9)
                    {
                        viewDirection = XYZ.BasisY;
                        upDirection = XYZ.BasisZ;
                        rightDirection = XYZ.BasisX;
                    }
                    // Для горизонтальных труб
                    else
                    {
                        viewDirection = pipeDirection.CrossProduct(XYZ.BasisZ).Normalize();
                        if (viewDirection.IsZeroLength())
                            viewDirection = XYZ.BasisX;

                        upDirection = XYZ.BasisZ;
                        rightDirection = viewDirection.CrossProduct(upDirection).Normalize();
                    }

                    // Настраиваем преобразование
                    Transform transform = Transform.Identity;
                    transform.Origin = midpoint;
                    transform.BasisX = rightDirection;
                    transform.BasisY = upDirection;
                    transform.BasisZ = -viewDirection; // Отрицательное значение для правильного направления взгляда

                    sectionBox.Transform = transform;

                    // Создаем вид разреза
                    ViewSection sectionView = ViewSection.CreateSection(doc,
                        doc.GetDefaultElementTypeId(ElementTypeGroup.ViewTypeSection),
                        sectionBox);

                    if (sectionView == null)
                    {
                        message = "Не удалось создать вид разреза.";
                        return Result.Failed;
                    }

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
                message = ex.Message;
                TaskDialog.Show("Ошибка", ex.ToString());
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
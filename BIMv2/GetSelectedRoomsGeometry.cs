using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace BIMv2
{
    [Transaction(TransactionMode.Manual)]
    public class GetSelectedRoomsGeometry : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;
            UIDocument uiDoc = uiApp.ActiveUIDocument;
            Document doc = uiDoc.Document;

            try
            {
                // Получаем выделенные элементы
                ICollection<ElementId> selectedIds = uiDoc.Selection.GetElementIds();

                if (selectedIds.Count == 0)
                {
                    TaskDialog.Show("Информация", "Не выбрано ни одного элемента. Пожалуйста, выделите помещения.");
                    return Result.Failed;
                }

                // Фильтруем только помещения
                List<Room> selectedRooms = new List<Room>();
                foreach (ElementId id in selectedIds)
                {
                    Element element = doc.GetElement(id);
                    if (element is Room room)
                    {
                        selectedRooms.Add(room);
                    }
                }

                if (selectedRooms.Count == 0)
                {
                    TaskDialog.Show("Информация", "Среди выделенных элементов нет помещений. Пожалуйста, выделите помещения.");
                    return Result.Failed;
                }

                // Собираем данные только о контурах и сегментах
                StringBuilder resultMessage = new StringBuilder();
                resultMessage.AppendLine("========== КОНТУРЫ И СЕГМЕНТЫ ПОМЕЩЕНИЙ ==========");
                resultMessage.AppendLine($"Всего выделено помещений: {selectedRooms.Count}");
                resultMessage.AppendLine();
                resultMessage.AppendLine("--------------------------------------------------");

                int roomCounter = 0;
                foreach (Room room in selectedRooms)
                {
                    roomCounter++;
                    resultMessage.AppendLine($"ПОМЕЩЕНИЕ #{roomCounter}:");
                    resultMessage.AppendLine($"  ID: {room.Id.IntegerValue}");
                    resultMessage.AppendLine($"  Имя: {room.Name}");
                    resultMessage.AppendLine($"  Номер: {room.Number}");

                    // Получаем границы помещения (контур)
                    SpatialElementBoundaryOptions options = new SpatialElementBoundaryOptions();
                    options.SpatialElementBoundaryLocation = SpatialElementBoundaryLocation.Finish;

                    IList<IList<BoundarySegment>> boundaries = room.GetBoundarySegments(options);

                    if (boundaries != null && boundaries.Count > 0)
                    {
                        int boundaryCounter = 0;
                        int totalSegments = 0;

                        foreach (IList<BoundarySegment> boundary in boundaries)
                        {
                            boundaryCounter++;
                            if (boundary != null)
                            {
                                resultMessage.AppendLine($"  Контур #{boundaryCounter} (сегментов: {boundary.Count}):");

                                int segmentCounter = 0;
                                foreach (BoundarySegment segment in boundary)
                                {
                                    segmentCounter++;
                                    Curve curve = segment.GetCurve();
                                    XYZ startPoint = curve.GetEndPoint(0);
                                    XYZ endPoint = curve.GetEndPoint(1);
                                    double length = curve.Length;

                                    totalSegments++;

                                    resultMessage.AppendLine($"    Сегмент #{segmentCounter}:");
                                    resultMessage.AppendLine($"      Длина: {length:F3} м");
                                    resultMessage.AppendLine($"      Начало: ({startPoint.X:F3}, {startPoint.Y:F3}, {startPoint.Z:F3})");
                                    resultMessage.AppendLine($"      Конец: ({endPoint.X:F3}, {endPoint.Y:F3}, {endPoint.Z:F3})");
                                }
                            }
                        }

                        resultMessage.AppendLine($"  Всего сегментов: {totalSegments}");
                    }
                    else
                    {
                        resultMessage.AppendLine("  Внимание: Не удалось получить границы помещения");
                    }

                    resultMessage.AppendLine("--------------------------------------------------");
                }

                // Создаем диалог с кнопкой копирования
                string fullMessage = resultMessage.ToString();

                // Используем TaskDialog с командами
                TaskDialog dialog = new TaskDialog("Контуры и сегменты помещений");
                dialog.MainInstruction = "Получены данные о контурах выделенных помещений";
                dialog.MainContent = "Нажмите 'Копировать в буфер обмена', чтобы скопировать все данные";
                dialog.ExpandedContent = fullMessage;
                dialog.FooterText = "Данные можно скопировать и вставить в любой текстовый редактор";

                // Добавляем кнопки
                dialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "Копировать в буфер обмена");
                dialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink2, "Закрыть");

                // Показываем диалог
                TaskDialogResult result = dialog.Show();

                // Обрабатываем результат
                if (result == TaskDialogResult.CommandLink1)
                {
                    try
                    {
                        Clipboard.SetText(fullMessage);
                        TaskDialog.Show("Успешно", "Данные скопированы в буфер обмена!");
                    }
                    catch (Exception ex)
                    {
                        TaskDialog.Show("Ошибка", $"Не удалось скопировать данные: {ex.Message}");
                    }
                }

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Ошибка", $"Не удалось выполнить операцию: {ex.Message}\n\n{ex.StackTrace}");
                return Result.Failed;
            }
        }
    }
}
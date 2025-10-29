using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;

namespace RoomCopyPlugin
{
    [Transaction(TransactionMode.Manual)]
    public class CopyRoomsFromLinkCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;
            UIDocument uiDoc = uiApp.ActiveUIDocument;
            Document doc = uiDoc.Document;

            try
            {
                // Получаем все связанные модели
                var collector = new FilteredElementCollector(doc);
                var linkInstances = collector.OfClass(typeof(RevitLinkInstance))
                    .Cast<RevitLinkInstance>()
                    .Where(link => link.GetLinkDocument() != null)
                    .ToList();

                if (!linkInstances.Any())
                {
                    TaskDialog.Show("Информация", "В модели нет связанных файлов Revit.");
                    return Result.Cancelled;
                }

                // Показываем диалог выбора связанной модели
                RevitLinkInstance selectedLink = ShowLinkSelectionDialog(linkInstances);

                if (selectedLink != null)
                {
                    CopyRoomsFromLink(doc, selectedLink);
                    return Result.Succeeded;
                }

                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("Ошибка", $"Произошла ошибка: {ex.Message}");
                return Result.Failed;
            }
        }

        private RevitLinkInstance ShowLinkSelectionDialog(List<RevitLinkInstance> linkInstances)
        {
            // Создаем диалог выбора
            var selectionDialog = new TaskDialog("Выбор связанной модели");
            selectionDialog.MainInstruction = "Выберите связанную модель для копирования помещений:";
            selectionDialog.MainContent = $"Найдено связанных моделей: {linkInstances.Count}";
            selectionDialog.CommonButtons = TaskDialogCommonButtons.Cancel;

            // Добавляем кнопки для каждой связанной модели
            for (int i = 0; i < linkInstances.Count; i++)
            {
                string linkName = linkInstances[i].Name;
                if (string.IsNullOrEmpty(linkName))
                    linkName = "Без имени";

                selectionDialog.AddCommandLink(
                    (TaskDialogCommandLinkId)((int)TaskDialogCommandLinkId.CommandLink1 + i),
                    linkName);
            }

            // Показываем диалог и обрабатываем результат
            TaskDialogResult result = selectionDialog.Show();

            if (result == TaskDialogResult.CommandLink1)
            {
                return linkInstances[0];
            }
            else if (result >= TaskDialogResult.CommandLink1 && result <= TaskDialogResult.CommandLink2)
            {
                int selectedIndex = (int)result - (int)TaskDialogResult.CommandLink1;
                if (selectedIndex < linkInstances.Count)
                {
                    return linkInstances[selectedIndex];
                }
            }

            return null;
        }

        private void CopyRoomsFromLink(Document targetDoc, RevitLinkInstance linkInstance)
        {
            Document linkDoc = linkInstance.GetLinkDocument();
            if (linkDoc == null) return;

            Transform linkTransform = linkInstance.GetTotalTransform();

            using (Transaction trans = new Transaction(targetDoc, "Копирование помещений из связи"))
            {
                trans.Start();

                // Получаем первый уровень в текущей модели для создания помещений
                Level targetLevel = GetFirstLevel(targetDoc);
                if (targetLevel == null)
                {
                    TaskDialog.Show("Ошибка", "В текущей модели нет уровней.");
                    trans.RollBack();
                    return;
                }

                // Получаем все помещения из связанной модели
                var roomCollector = new FilteredElementCollector(linkDoc);
                var sourceRooms = roomCollector.OfClass(typeof(Room))
                    .Cast<Room>()
                    .Where(r => r.Area > 0 && r.Level != null) // Только помещения с площадью и уровнем
                    .ToList();

                int copiedCount = 0;

                foreach (var sourceRoom in sourceRooms)
                {
                    try
                    {
                        // Получаем расположение помещения из связанной модели
                        Location roomLocation = sourceRoom.Location;
                        if (roomLocation == null) continue;

                        XYZ roomPoint = GetRoomLocationPoint(roomLocation);
                        if (roomPoint == null) continue;

                        // Преобразуем точку в координаты текущей модели
                        XYZ transformedPoint = linkTransform.OfPoint(roomPoint);

                        // Создаем новое помещение на целевом уровне
                        Room newRoom = targetDoc.Create.NewRoom(targetLevel,
                            new UV(transformedPoint.X, transformedPoint.Y));

                        if (newRoom == null) continue;

                        // Копируем основные свойства
                        if (!string.IsNullOrEmpty(sourceRoom.Name))
                            newRoom.Name = sourceRoom.Name;

                        if (!string.IsNullOrEmpty(sourceRoom.Number))
                            newRoom.Number = sourceRoom.Number;

                        // Копируем параметры
                        CopyParameters(sourceRoom, newRoom);
                        copiedCount++;
                    }
                    catch (Exception ex)
                    {
                        // Логируем ошибку, но продолжаем обработку
                        Debug.WriteLine($"Ошибка копирования помещения: {ex.Message}");
                        continue;
                    }
                }

                trans.Commit();

                TaskDialog.Show("Результат",
                    $"Скопировано помещений: {copiedCount} из {sourceRooms.Count}");
            }
        }

        private Level GetFirstLevel(Document doc)
        {
            var levelCollector = new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .OrderBy(l => l.Elevation)
                .FirstOrDefault();

            return levelCollector;
        }

        private XYZ GetRoomLocationPoint(Location location)
        {
            if (location is LocationPoint locationPoint)
            {
                return locationPoint.Point;
            }
            else if (location is LocationCurve locationCurve)
            {
                Curve curve = locationCurve.Curve;
                return curve.GetEndPoint(0); // Возвращаем первую точку кривой
            }

            return XYZ.Zero;
        }

        private void CopyParameters(Element source, Element target)
        {
            foreach (Parameter sourceParam in source.Parameters)
            {
                if (sourceParam.IsReadOnly) continue;

                Parameter targetParam = target.LookupParameter(sourceParam.Definition.Name);
                if (targetParam == null || targetParam.IsReadOnly) continue;

                try
                {
                    switch (sourceParam.StorageType)
                    {
                        case StorageType.Double:
                            targetParam.Set(sourceParam.AsDouble());
                            break;
                        case StorageType.Integer:
                            targetParam.Set(sourceParam.AsInteger());
                            break;
                        case StorageType.String:
                            targetParam.Set(sourceParam.AsString());
                            break;
                        case StorageType.ElementId:
                            targetParam.Set(sourceParam.AsElementId());
                            break;
                    }
                }
                catch
                {
                    // Игнорируем ошибки установки параметров
                }
            }
        }
    }

    // Класс приложения для регистрации в Revit
    public class Application : IExternalApplication
    {
        public Result OnStartup(UIControlledApplication app)
        {
            try
            {
                // Создаем панель инструментов
                RibbonPanel panel = app.CreateRibbonPanel("Копирование помещений");

                // Создаем кнопку
                PushButtonData buttonData = new PushButtonData(
                    "CopyRooms",
                    "Копировать\nпомещения",
                    System.Reflection.Assembly.GetExecutingAssembly().Location,
                    "RoomCopyPlugin.CopyRoomsFromLinkCommand");

                buttonData.ToolTip = "Копирование помещений из связанной модели";
                buttonData.LongDescription = "Копирует помещения из выбранной связанной модели в текущую модель";

                PushButton button = panel.AddItem(buttonData) as PushButton;

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Ошибка инициализации", ex.Message);
                return Result.Failed;
            }
        }

        public Result OnShutdown(UIControlledApplication app)
        {
            return Result.Succeeded;
        }
    }
}
#region Namespace

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;

using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;

#endregion

namespace BIMv2
{
    [Transaction(TransactionMode.Manual)]
    public class MoveAllRoomTagsSmart : IExternalCommand
    {
        // Значения по умолчанию
        private double _offsetXmm = 15.0; // Отступ по X (горизонтальный)
        private double _offsetYmm = 15.0; // Отступ по Y (вертикальный)

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            // Показываем форму для настройки отступов
            using (OffsetSettingsForm settingsForm = new OffsetSettingsForm(_offsetXmm, _offsetYmm))
            {
                if (settingsForm.ShowDialog() == DialogResult.OK)
                {
                    _offsetXmm = settingsForm.OffsetX;
                    _offsetYmm = settingsForm.OffsetY;
                }
                else
                {
                    return Result.Cancelled; // Пользователь отменил
                }
            }

            UIDocument uiDoc = commandData.Application.ActiveUIDocument;
            Document doc = uiDoc.Document;
            Autodesk.Revit.DB.View view = doc.ActiveView;

            // Конвертация мм в футы
            double offsetXft = _offsetXmm / 304.8;
            double offsetYft = _offsetYmm / 304.8;

            using (Transaction t = new Transaction(doc, $"Перемещение марок (X:{_offsetXmm}мм Y:{_offsetYmm}мм)"))
            {
                t.Start();

                try
                {
                    // Собираем все марки
                    var tags = new FilteredElementCollector(doc, view.Id)
                        .OfCategory(BuiltInCategory.OST_RoomTags)
                        .WhereElementIsNotElementType()
                        .ToList();

                    int moved = 0;
                    int failed = 0;
                    List<string> failedDetails = new List<string>();

                    foreach (Element tag in tags)
                    {
                        string result = ProcessTag(tag, offsetXft, offsetYft);
                        if (result == "OK")
                        {
                            moved++;
                        }
                        else
                        {
                            failed++;
                            failedDetails.Add(result);
                        }
                    }

                    t.Commit();

                    // Показываем результаты
                    ShowResults(moved, failed, failedDetails);

                    return Result.Succeeded;
                }
                catch (Exception ex)
                {
                    message = ex.Message;
                    t.RollBack();
                    return Result.Failed;
                }
            }
        }

        private string ProcessTag(Element tag, double offsetXft, double offsetYft)
        {
            try
            {
                // Текущее положение марки
                LocationPoint tagLoc = tag.Location as LocationPoint;
                if (tagLoc == null) return "Неверный тип расположения марки";

                XYZ currentPoint = tagLoc.Point;

                // Ищем помещение
                Room room = FindRoomContainingPoint(tag.Document, currentPoint);
                if (room == null) return "Не найдено помещение";

                // Получаем все уникальные углы помещения
                List<XYZ> corners = GetAllRoomCorners(room);
                if (corners.Count == 0) return "Не удалось получить углы помещения";

                // Находим самую правую точку
                double maxX = corners.Max(p => p.X);
                var rightmostCorners = corners.Where(p => Math.Abs(p.X - maxX) < 0.001).ToList();

                // Среди самых правых находим самую нижнюю
                double minY = rightmostCorners.Min(p => p.Y);
                var bottomRightCorners = rightmostCorners.Where(p => Math.Abs(p.Y - minY) < 0.001).ToList();

                XYZ targetCorner = bottomRightCorners.First();

                // Ищем точку внутри помещения рядом с этим углом С ПРИМЕНЕНИЕМ ОТСТУПА
                XYZ insidePoint = FindInsidePointWithOffset(room, targetCorner, offsetXft, offsetYft);
                if (insidePoint == null) return "Не найдена точка внутри помещения";

                // Перемещаем марку
                ElementTransformUtils.MoveElement(tag.Document, tag.Id, insidePoint - currentPoint);

                return "OK";
            }
            catch (Exception ex)
            {
                return $"Ошибка: {ex.Message}";
            }
        }

        private List<XYZ> GetAllRoomCorners(Room room)
        {
            List<XYZ> allPoints = new List<XYZ>();

            try
            {
                var boundaries = room.GetBoundarySegments(new SpatialElementBoundaryOptions());
                if (boundaries == null) return allPoints;

                // Собираем все конечные точки кривых
                foreach (var boundary in boundaries)
                {
                    foreach (var segment in boundary)
                    {
                        Curve curve = segment.GetCurve();
                        if (curve != null)
                        {
                            allPoints.Add(curve.GetEndPoint(0));
                            allPoints.Add(curve.GetEndPoint(1));
                        }
                    }
                }

                // Убираем дубликаты
                return RemoveDuplicates(allPoints);
            }
            catch
            {
                return allPoints;
            }
        }

        private List<XYZ> RemoveDuplicates(List<XYZ> points)
        {
            List<XYZ> unique = new List<XYZ>();

            foreach (XYZ point in points)
            {
                if (!unique.Any(p =>
                    Math.Abs(p.X - point.X) < 0.001 &&
                    Math.Abs(p.Y - point.Y) < 0.001 &&
                    Math.Abs(p.Z - point.Z) < 0.001))
                {
                    unique.Add(point);
                }
            }

            return unique;
        }

        /// <summary>
        /// Находит точку внутри помещения с применением отступа от угла
        /// </summary>
        private XYZ FindInsidePointWithOffset(Room room, XYZ corner, double offsetXft, double offsetYft)
        {
            // Сначала найдем точку внутри помещения без отступа
            XYZ pointInside = FindPointInsideRoom(corner, room);
            if (pointInside == null) return null;

            // Теперь попробуем применить отступ от этой точки
            XYZ pointWithOffset = new XYZ(
                pointInside.X - offsetXft,
                pointInside.Y + offsetYft,
                pointInside.Z);

            // Проверяем, остается ли точка с отступом внутри помещения
            if (room.IsPointInRoom(pointWithOffset))
            {
                return pointWithOffset;
            }

            // Если точка с отступом не внутри, ищем альтернативную точку
            return FindAlternativePointWithOffset(room, pointInside, offsetXft, offsetYft);
        }

        /// <summary>
        /// Находит точку внутри помещения (без отступа)
        /// </summary>
        private XYZ FindPointInsideRoom(XYZ startPoint, Room room)
        {
            // Если стартовая точка уже внутри
            if (room.IsPointInRoom(startPoint))
            {
                return startPoint;
            }

            // Ищем по спирали от стартовой точки
            double searchStep = 0.05; // Меньший шаг для более точного поиска
            int maxRadius = 40; // Максимальный радиус поиска (2 фута)

            for (int radius = 1; radius <= maxRadius; radius++)
            {
                double radiusFt = radius * searchStep;

                // Проверяем точки на квадрате
                for (int dx = -radius; dx <= radius; dx++)
                {
                    for (int dy = -radius; dy <= radius; dy++)
                    {
                        // Пропускаем внутренние точки (только границы спирали)
                        if (Math.Abs(dx) != radius && Math.Abs(dy) != radius)
                            continue;

                        double offsetX = dx * searchStep;
                        double offsetY = dy * searchStep;

                        XYZ testPoint = new XYZ(
                            startPoint.X + offsetX,
                            startPoint.Y + offsetY,
                            startPoint.Z);

                        if (room.IsPointInRoom(testPoint))
                        {
                            return testPoint;
                        }
                    }
                }
            }

            // Если не нашли, пробуем центр помещения
            return GetRoomCenter(room);
        }

        /// <summary>
        /// Находит альтернативную точку с отступом
        /// </summary>
        private XYZ FindAlternativePointWithOffset(Room room, XYZ basePoint, double offsetXft, double offsetYft)
        {
            // Пробуем разные направления для применения отступа
            double[][] directions =
            {
                new double[] { -offsetXft, offsetYft },  // стандартное направление
                new double[] { -offsetXft * 0.5, offsetYft * 0.5 },  // половинный отступ
                new double[] { -offsetXft * 0.8, offsetYft * 0.8 },  // 80% отступа
                new double[] { -offsetXft * 0.3, offsetYft * 0.3 },  // 30% отступа
                new double[] { 0, 0 }  // без отступа
            };

            foreach (var direction in directions)
            {
                XYZ testPoint = new XYZ(
                    basePoint.X + direction[0],
                    basePoint.Y + direction[1],
                    basePoint.Z);

                if (room.IsPointInRoom(testPoint))
                {
                    return testPoint;
                }
            }

            // Если ничего не подошло, ищем другую базовую точку
            return FindBestPointForOffset(room, basePoint, offsetXft, offsetYft);
        }

        /// <summary>
        /// Находит лучшую точку для применения отступа
        /// </summary>
        private XYZ FindBestPointForOffset(Room room, XYZ referencePoint, double offsetXft, double offsetYft)
        {
            // Ищем точку, от которой можно отступить на заданное расстояние
            double searchStep = 0.05;
            int maxSteps = 30;

            // Пробуем двигаться от referencePoint в разных направлениях
            for (int steps = 1; steps <= maxSteps; steps++)
            {
                double moveDistance = steps * searchStep;

                // 8 основных направлений
                double[][] moveDirections =
                {
                    new double[] { -moveDistance, moveDistance },  // влево-вверх
                    new double[] { -moveDistance, 0 },             // влево
                    new double[] { 0, moveDistance },              // вверх
                    new double[] { -moveDistance * 0.7, moveDistance * 0.7 },
                    new double[] { -moveDistance, moveDistance * 0.3 },
                    new double[] { -moveDistance * 0.3, moveDistance }
                };

                foreach (var direction in moveDirections)
                {
                    XYZ testBasePoint = new XYZ(
                        referencePoint.X + direction[0],
                        referencePoint.Y + direction[1],
                        referencePoint.Z);

                    if (room.IsPointInRoom(testBasePoint))
                    {
                        // Пробуем применить отступ от этой точки
                        XYZ pointWithOffset = new XYZ(
                            testBasePoint.X - offsetXft,
                            testBasePoint.Y + offsetYft,
                            testBasePoint.Z);

                        if (room.IsPointInRoom(pointWithOffset))
                        {
                            return pointWithOffset;
                        }
                    }
                }
            }

            // Если не нашли подходящую точку, возвращаем просто точку внутри без отступа
            return FindPointInsideRoom(referencePoint, room);
        }

        private XYZ GetRoomCenter(Room room)
        {
            try
            {
                if (room.Location is LocationPoint locationPoint)
                {
                    return locationPoint.Point;
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        private Room FindRoomContainingPoint(Document doc, XYZ point)
        {
            var rooms = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_Rooms)
                .WhereElementIsNotElementType()
                .Cast<Room>();

            return rooms.FirstOrDefault(r => r.IsPointInRoom(point));
        }

        private void ShowResults(int moved, int failed, List<string> failedDetails)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"Отступ по X: {_offsetXmm} мм");
            sb.AppendLine($"Отступ по Y: {_offsetYmm} мм");
            sb.AppendLine($"Успешно перемещено: {moved}");
            sb.AppendLine($"Не удалось: {failed}");

            if (failed > 0 && failedDetails.Count > 0)
            {
                sb.AppendLine("\nОшибки:");
                int showCount = Math.Min(failedDetails.Count, 10);
                for (int i = 0; i < showCount; i++)
                {
                    string shortMsg = failedDetails[i].Length > 50 ?
                        failedDetails[i].Substring(0, 50) + "..." : failedDetails[i];
                    sb.AppendLine($"  • {shortMsg}");
                }

                if (failedDetails.Count > 10)
                {
                    sb.AppendLine($"  ... и еще {failedDetails.Count - 10} ошибок");
                }
            }

            TaskDialog.Show("Результат", sb.ToString());
        }
    }

    /// <summary>
    /// Форма для настройки отступов
    /// </summary>
    public class OffsetSettingsForm : System.Windows.Forms.Form
    {
        private NumericUpDown _numOffsetX;
        private NumericUpDown _numOffsetY;
        private Button _btnOk;
        private Button _btnCancel;

        public double OffsetX { get; private set; }
        public double OffsetY { get; private set; }

        public OffsetSettingsForm(double defaultOffsetX, double defaultOffsetY)
        {
            InitializeComponent();
            OffsetX = defaultOffsetX;
            OffsetY = defaultOffsetY;

            _numOffsetX.Value = (decimal)defaultOffsetX;
            _numOffsetY.Value = (decimal)defaultOffsetY;
        }

        private void InitializeComponent()
        {
            this.Text = "Настройка отступов марки";
            this.Size = new System.Drawing.Size(650, 420);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            // Заголовок
            Label lblTitle = new Label
            {
                Text = "Укажите отступы марки от правого нижнего угла помещения:",
                Location = new System.Drawing.Point(20, 20),
                Size = new System.Drawing.Size(300, 40),
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft
            };

            // Отступ по X
            Label lblOffsetX = new Label
            {
                Text = "Отступ по X (влево от правой границы):",
                Location = new System.Drawing.Point(20, 70),
                Size = new System.Drawing.Size(200, 20)
            };

            _numOffsetX = new NumericUpDown
            {
                Minimum = 0,
                Maximum = 500,
                Value = 150,
                Location = new System.Drawing.Point(230, 68),
                Size = new System.Drawing.Size(80, 20),
                DecimalPlaces = 1,
                Increment = 1
            };

            Label lblMmX = new Label
            {
                Text = "мм",
                Location = new System.Drawing.Point(315, 70),
                Size = new System.Drawing.Size(30, 20)
            };

            // Отступ по Y
            Label lblOffsetY = new Label
            {
                Text = "Отступ по Y (вверх от нижней границы):",
                Location = new System.Drawing.Point(20, 100),
                Size = new System.Drawing.Size(200, 20)
            };

            _numOffsetY = new NumericUpDown
            {
                Minimum = 0,
                Maximum = 500,
                Value = 150,
                Location = new System.Drawing.Point(230, 98),
                Size = new System.Drawing.Size(80, 20),
                DecimalPlaces = 1,
                Increment = 1
            };

            Label lblMmY = new Label
            {
                Text = "мм",
                Location = new System.Drawing.Point(315, 100),
                Size = new System.Drawing.Size(30, 20)
            };

            // Пояснение
            Label lblNote = new Label
            {
                Text = "Отступ применяется ОТ найденной точки внутри помещения\nЕсли точка слишком близко к границе, отступ может не применяться",
                Location = new System.Drawing.Point(20, 130),
                Size = new System.Drawing.Size(300, 40),
                Font = new System.Drawing.Font("Arial", 8),
                ForeColor = System.Drawing.Color.DarkGray
            };

            // Кнопки
            _btnOk = new Button
            {
                Text = "Применить",
                Location = new System.Drawing.Point(160, 180),
                Size = new System.Drawing.Size(80, 25),
                DialogResult = DialogResult.OK
            };

            _btnCancel = new Button
            {
                Text = "Отмена",
                Location = new System.Drawing.Point(250, 180),
                Size = new System.Drawing.Size(80, 25),
                DialogResult = DialogResult.Cancel
            };

            // Добавляем элементы на форму
            this.Controls.Add(lblTitle);
            this.Controls.Add(lblOffsetX);
            this.Controls.Add(_numOffsetX);
            this.Controls.Add(lblMmX);
            this.Controls.Add(lblOffsetY);
            this.Controls.Add(_numOffsetY);
            this.Controls.Add(lblMmY);
            this.Controls.Add(lblNote);
            this.Controls.Add(_btnOk);
            this.Controls.Add(_btnCancel);

            // Назначаем кнопки по умолчанию
            this.AcceptButton = _btnOk;
            this.CancelButton = _btnCancel;

            // Обработчики событий
            _btnOk.Click += BtnOk_Click;
        }

        private void BtnOk_Click(object sender, EventArgs e)
        {
            OffsetX = (double)_numOffsetX.Value;
            OffsetY = (double)_numOffsetY.Value;
        }
    }
}
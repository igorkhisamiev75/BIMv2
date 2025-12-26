#region Namespace

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Drawing; // Добавляем это

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
        // Значения по умолчанию для масштаба 1:100
        private double _offsetXmm = 1500.0; // 1.5 метра для масштаба 1:100
        private double _offsetYmm = 1500.0; // 1.5 метра для масштаба 1:100

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uiDoc = commandData.Application.ActiveUIDocument;
            Document doc = uiDoc.Document;
            Autodesk.Revit.DB.View view = doc.ActiveView;

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

                // Ищем точку внутри помещения с учетом отступа
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
        /// Находит точку внутри помещения с применением отступа
        /// </summary>
        private XYZ FindInsidePointWithOffset(Room room, XYZ corner, double offsetXft, double offsetYft)
        {
            // Пробуем сразу точку с отступом
            XYZ pointWithOffset = new XYZ(
                corner.X - offsetXft,
                corner.Y + offsetYft,
                corner.Z);

            if (room.IsPointInRoom(pointWithOffset))
            {
                return pointWithOffset;
            }

            // Если точка с отступом не внутри, ищем альтернативу
            return FindBestPointWithOffset(room, corner, offsetXft, offsetYft);
        }

        /// <summary>
        /// Находит лучшую точку с учетом отступа
        /// </summary>
        private XYZ FindBestPointWithOffset(Room room, XYZ corner, double offsetXft, double offsetYft)
        {
            // Пробуем разные проценты от желаемого отступа
            double[] percentages = { 1.0, 0.9, 0.8, 0.7, 0.6, 0.5, 0.4, 0.3, 0.2, 0.1, 0.0 };

            foreach (double percentage in percentages)
            {
                double appliedOffsetX = offsetXft * percentage;
                double appliedOffsetY = offsetYft * percentage;

                XYZ testPoint = new XYZ(
                    corner.X - appliedOffsetX,
                    corner.Y + appliedOffsetY,
                    corner.Z);

                if (room.IsPointInRoom(testPoint))
                {
                    return testPoint;
                }
            }

            // Если не нашли, используем центр помещения
            return GetRoomCenter(room);
        }

        private XYZ GetRoomCenter(Room room)
        {
            try
            {
                if (room.Location is LocationPoint locationPoint)
                {
                    return locationPoint.Point;
                }

                // Альтернативный способ через bounding box
                BoundingBoxXYZ bbox = room.get_BoundingBox(null);
                if (bbox != null)
                {
                    return new XYZ(
                        (bbox.Min.X + bbox.Max.X) / 2,
                        (bbox.Min.Y + bbox.Max.Y) / 2,
                        bbox.Min.Z);
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
            sb.AppendLine($"Отступ по X: {_offsetXmm} мм ({_offsetXmm / 1000.0:F2} м)");
            sb.AppendLine($"Отступ по Y: {_offsetYmm} мм ({_offsetYmm / 1000.0:F2} м)");
            sb.AppendLine($"Успешно перемещено: {moved}");
            sb.AppendLine($"Не удалось: {failed}");

            if (failed > 0 && failedDetails.Count > 0)
            {
                sb.AppendLine("\nОсновные ошибки:");
                int showCount = Math.Min(failedDetails.Count, 5);
                for (int i = 0; i < showCount; i++)
                {
                    string shortMsg = failedDetails[i].Length > 60 ?
                        failedDetails[i].Substring(0, 60) + "..." : failedDetails[i];
                    sb.AppendLine($"  • {shortMsg}");
                }

                if (failedDetails.Count > 5)
                {
                    sb.AppendLine($"  ... и еще {failedDetails.Count - 5} ошибок");
                }
            }

            sb.AppendLine("\nПримечание: Для маленьких помещений полный отступ может не применяться.");

            TaskDialog.Show("Результат перемещения марок", sb.ToString());
        }
    }

    /// <summary>
    /// Форма для настройки отступов (упрощенная без графики)
    /// </summary>
    public class OffsetSettingsForm : System.Windows.Forms.Form
    {
        private NumericUpDown _numOffsetX;
        private NumericUpDown _numOffsetY;
        private Button _btnOk;
        private Button _btnCancel;
        private Label _lblPreviewX;
        private Label _lblPreviewY;

        public double OffsetX { get; private set; }
        public double OffsetY { get; private set; }

        public OffsetSettingsForm(double defaultOffsetX, double defaultOffsetY)
        {
            InitializeComponent();
            OffsetX = defaultOffsetX;
            OffsetY = defaultOffsetY;

            _numOffsetX.Value = (decimal)defaultOffsetX;
            _numOffsetY.Value = (decimal)defaultOffsetY;

            UpdatePreview();
        }

        private void InitializeComponent()
        {
            this.Text = "Настройка отступов марки";
            this.Size = new System.Drawing.Size(1000, 650);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            // Заголовок
            Label lblTitle = new Label
            {
                Text = "Укажите отступы марки от правого нижнего угла помещения:",
                Location = new System.Drawing.Point(20, 20),
                Size = new System.Drawing.Size(450, 30),
                Font = new System.Drawing.Font("Arial", 10, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            };

            // Пояснение
            Label lblExplanation = new Label
            {
                Text = "X - отступ влево от правой границы\nY - отступ вверх от нижней границы",
                Location = new System.Drawing.Point(20, 60),
                Size = new System.Drawing.Size(450, 40),
                Font = new System.Drawing.Font("Arial", 9),
                TextAlign = ContentAlignment.MiddleLeft
            };

            // Отступ по X
            Label lblOffsetX = new Label
            {
                Text = "Отступ по X (горизонтальный):",
                Location = new System.Drawing.Point(20, 120),
                Size = new System.Drawing.Size(200, 25),
                Font = new System.Drawing.Font("Arial", 9)
            };

            _numOffsetX = new NumericUpDown
            {
                Minimum = 0,
                Maximum = 10000,
                Value = 1500,
                Location = new System.Drawing.Point(230, 120),
                Size = new System.Drawing.Size(100, 20),
                DecimalPlaces = 0,
                Increment = 100
            };

            Label lblMmX = new Label
            {
                Text = "мм",
                Location = new System.Drawing.Point(340, 122),
                Size = new System.Drawing.Size(30, 20),
                Font = new System.Drawing.Font("Arial", 9)
            };

            _lblPreviewX = new Label
            {
                Text = "",
                Location = new System.Drawing.Point(380, 122),
                Size = new System.Drawing.Size(100, 20),
                Font = new System.Drawing.Font("Arial", 9),
                ForeColor = System.Drawing.Color.Blue
            };

            // Отступ по Y
            Label lblOffsetY = new Label
            {
                Text = "Отступ по Y (вертикальный):",
                Location = new System.Drawing.Point(20, 160),
                Size = new System.Drawing.Size(200, 25),
                Font = new System.Drawing.Font("Arial", 9)
            };

            _numOffsetY = new NumericUpDown
            {
                Minimum = 0,
                Maximum = 10000,
                Value = 1500,
                Location = new System.Drawing.Point(230, 160),
                Size = new System.Drawing.Size(100, 20),
                DecimalPlaces = 0,
                Increment = 100
            };

            Label lblMmY = new Label
            {
                Text = "мм",
                Location = new System.Drawing.Point(340, 162),
                Size = new System.Drawing.Size(30, 20),
                Font = new System.Drawing.Font("Arial", 9)
            };

            _lblPreviewY = new Label
            {
                Text = "",
                Location = new System.Drawing.Point(380, 162),
                Size = new System.Drawing.Size(100, 20),
                Font = new System.Drawing.Font("Arial", 9),
                ForeColor = System.Drawing.Color.Blue
            };

            // Рекомендации
            Label lblRecommendations = new Label
            {
                Text = "Рекомендации для масштаба 1:100:\n" +
                       "• 1500 мм = 1.5 м (стандартный)\n" +
                       "• 1000 мм = 1.0 м (маленький)\n" +
                       "• 2000 мм = 2.0 м (большой)\n" +
                       "•  500 мм = 0.5 м (очень маленький)",
                Location = new System.Drawing.Point(20, 200),
                Size = new System.Drawing.Size(300, 100),
                Font = new System.Drawing.Font("Arial", 9),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = System.Drawing.Color.LightYellow
            };

            // Кнопки
            _btnOk = new Button
            {
                Text = "Применить",
                Location = new System.Drawing.Point(300, 280),
                Size = new System.Drawing.Size(90, 30),
                DialogResult = DialogResult.OK,
                Font = new System.Drawing.Font("Arial", 9)
            };

            _btnCancel = new Button
            {
                Text = "Отмена",
                Location = new System.Drawing.Point(400, 280),
                Size = new System.Drawing.Size(90, 30),
                DialogResult = DialogResult.Cancel,
                Font = new System.Drawing.Font("Arial", 9)
            };

            // Добавляем элементы на форму
            this.Controls.Add(lblTitle);
            this.Controls.Add(lblExplanation);
            this.Controls.Add(lblOffsetX);
            this.Controls.Add(_numOffsetX);
            this.Controls.Add(lblMmX);
            this.Controls.Add(_lblPreviewX);
            this.Controls.Add(lblOffsetY);
            this.Controls.Add(_numOffsetY);
            this.Controls.Add(lblMmY);
            this.Controls.Add(_lblPreviewY);
            this.Controls.Add(lblRecommendations);
            this.Controls.Add(_btnOk);
            this.Controls.Add(_btnCancel);

            // Назначаем кнопки по умолчанию
            this.AcceptButton = _btnOk;
            this.CancelButton = _btnCancel;

            // Обработчики событий
            _btnOk.Click += BtnOk_Click;
            _numOffsetX.ValueChanged += NumOffset_ValueChanged;
            _numOffsetY.ValueChanged += NumOffset_ValueChanged;
        }

        private void BtnOk_Click(object sender, EventArgs e)
        {
            OffsetX = (double)_numOffsetX.Value;
            OffsetY = (double)_numOffsetY.Value;
        }

        private void NumOffset_ValueChanged(object sender, EventArgs e)
        {
            UpdatePreview();
        }

        private void UpdatePreview()
        {
            double offsetXmm = (double)_numOffsetX.Value;
            double offsetYmm = (double)_numOffsetY.Value;

            _lblPreviewX.Text = $"({offsetXmm / 1000.0:F2} м)";
            _lblPreviewY.Text = $"({offsetYmm / 1000.0:F2} м)";
        }
    }

    /// <summary>
    /// Форма для настройки отступов с предустановками
    /// </summary>
    public class AdvancedOffsetSettingsForm : System.Windows.Forms.Form
    {
        private NumericUpDown _numOffsetX;
        private NumericUpDown _numOffsetY;
        private Button _btnOk;
        private Button _btnCancel;
        private System.Windows.Forms.ComboBox _cmbPresets;
        private Label _lblPreviewX;
        private Label _lblPreviewY;
        private Label _lblDescription;

        // Предустановленные значения
        private Dictionary<string, (double X, double Y, string Description)> _presets =
            new Dictionary<string, (double X, double Y, string Description)>
        {
            {"Очень маленький", (500, 500, "500 мм = 0.5 м")},
            {"Маленький", (1000, 1000, "1000 мм = 1.0 м")},
            {"Стандартный", (1500, 1500, "1500 мм = 1.5 м")},
            {"Большой", (2000, 2000, "2000 мм = 2.0 м")},
            {"Очень большой", (3000, 3000, "3000 мм = 3.0 м")},
            {"Только по X", (1500, 0, "X: 1500 мм, Y: 0 мм")},
            {"Только по Y", (0, 1500, "X: 0 мм, Y: 1500 мм")},
            {"Пользовательский", (1500, 1500, "Задать свои значения")}
        };

        public double OffsetX { get; private set; }
        public double OffsetY { get; private set; }

        public AdvancedOffsetSettingsForm(double defaultOffsetX, double defaultOffsetY)
        {
            InitializeComponent();
            OffsetX = defaultOffsetX;
            OffsetY = defaultOffsetY;

            _numOffsetX.Value = (decimal)defaultOffsetX;
            _numOffsetY.Value = (decimal)defaultOffsetY;

            // Ищем подходящую предустановку
            string matchedPreset = "Пользовательский";
            foreach (var preset in _presets)
            {
                if (Math.Abs(preset.Value.X - defaultOffsetX) < 1 &&
                    Math.Abs(preset.Value.Y - defaultOffsetY) < 1)
                {
                    matchedPreset = preset.Key;
                    break;
                }
            }

            _cmbPresets.SelectedItem = matchedPreset;
            UpdatePreview();
            UpdateDescription();
        }

        private void InitializeComponent()
        {
            this.Text = "Настройка отступов марки";
            this.Size = new System.Drawing.Size(500, 400);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            // Заголовок
            Label lblTitle = new Label
            {
                Text = "Настройка отступов от правого нижнего угла помещения",
                Location = new System.Drawing.Point(20, 20),
                Size = new System.Drawing.Size(450, 30),
                Font = new System.Drawing.Font("Arial", 11, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            };

            // Предустановки
            Label lblPresets = new Label
            {
                Text = "Предустановки:",
                Location = new System.Drawing.Point(20, 70),
                Size = new System.Drawing.Size(100, 25),
                Font = new System.Drawing.Font("Arial", 9)
            };

            _cmbPresets = new System.Windows.Forms.ComboBox
            {
                Location = new System.Drawing.Point(130, 70),
                Size = new System.Drawing.Size(200, 20),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new System.Drawing.Font("Arial", 9)
            };

            _cmbPresets.Items.AddRange(_presets.Keys.ToArray());

            // Описание предустановки
            _lblDescription = new Label
            {
                Text = "",
                Location = new System.Drawing.Point(130, 100),
                Size = new System.Drawing.Size(300, 20),
                Font = new System.Drawing.Font("Arial", 8),
                ForeColor = System.Drawing.Color.DarkGreen
            };

            // Отступ по X
            Label lblOffsetX = new Label
            {
                Text = "Отступ по X (влево):",
                Location = new System.Drawing.Point(20, 140),
                Size = new System.Drawing.Size(150, 25),
                Font = new System.Drawing.Font("Arial", 9)
            };

            _numOffsetX = new NumericUpDown
            {
                Minimum = 0,
                Maximum = 10000,
                Value = 1500,
                Location = new System.Drawing.Point(180, 140),
                Size = new System.Drawing.Size(100, 20),
                DecimalPlaces = 0,
                Increment = 100,
                Font = new System.Drawing.Font("Arial", 9)
            };

            Label lblMmX = new Label
            {
                Text = "мм",
                Location = new System.Drawing.Point(290, 142),
                Size = new System.Drawing.Size(30, 20),
                Font = new System.Drawing.Font("Arial", 9)
            };

            _lblPreviewX = new Label
            {
                Text = "",
                Location = new System.Drawing.Point(330, 142),
                Size = new System.Drawing.Size(100, 20),
                Font = new System.Drawing.Font("Arial", 9),
                ForeColor = System.Drawing.Color.Blue
            };

            // Отступ по Y
            Label lblOffsetY = new Label
            {
                Text = "Отступ по Y (вверх):",
                Location = new System.Drawing.Point(20, 180),
                Size = new System.Drawing.Size(150, 25),
                Font = new System.Drawing.Font("Arial", 9)
            };

            _numOffsetY = new NumericUpDown
            {
                Minimum = 0,
                Maximum = 10000,
                Value = 1500,
                Location = new System.Drawing.Point(180, 180),
                Size = new System.Drawing.Size(100, 20),
                DecimalPlaces = 0,
                Increment = 100,
                Font = new System.Drawing.Font("Arial", 9)
            };

            Label lblMmY = new Label
            {
                Text = "мм",
                Location = new System.Drawing.Point(290, 182),
                Size = new System.Drawing.Size(30, 20),
                Font = new System.Drawing.Font("Arial", 9)
            };

            _lblPreviewY = new Label
            {
                Text = "",
                Location = new System.Drawing.Point(330, 182),
                Size = new System.Drawing.Size(100, 20),
                Font = new System.Drawing.Font("Arial", 9),
                ForeColor = System.Drawing.Color.Blue
            };

            // Пояснение
            Label lblNote = new Label
            {
                Text = "Примечание:\n" +
                       "• Для маленьких помещений отступ может быть автоматически уменьшен\n" +
                       "• X - отступ влево от правой границы\n" +
                       "• Y - отступ вверх от нижней границы",
                Location = new System.Drawing.Point(20, 220),
                Size = new System.Drawing.Size(450, 80),
                Font = new System.Drawing.Font("Arial", 9),
                ForeColor = System.Drawing. Color.DarkGray
            };

            // Кнопки
            _btnOk = new Button
            {
                Text = "Применить",
                Location = new System.Drawing.Point(300, 320),
                Size = new System.Drawing.Size(90, 30),
                DialogResult = DialogResult.OK,
                Font = new System.Drawing.Font("Arial", 9)
            };

            _btnCancel = new Button
            {
                Text = "Отмена",
                Location = new System.Drawing.Point(400, 320),
                Size = new System.Drawing.Size(90, 30),
                DialogResult = DialogResult.Cancel,
                Font = new System.Drawing.Font("Arial", 9)
            };

            // Добавляем элементы на форму
            this.Controls.Add(lblTitle);
            this.Controls.Add(lblPresets);
            this.Controls.Add(_cmbPresets);
            this.Controls.Add(_lblDescription);
            this.Controls.Add(lblOffsetX);
            this.Controls.Add(_numOffsetX);
            this.Controls.Add(lblMmX);
            this.Controls.Add(_lblPreviewX);
            this.Controls.Add(lblOffsetY);
            this.Controls.Add(_numOffsetY);
            this.Controls.Add(lblMmY);
            this.Controls.Add(_lblPreviewY);
            this.Controls.Add(lblNote);
            this.Controls.Add(_btnOk);
            this.Controls.Add(_btnCancel);

            // Назначаем кнопки по умолчанию
            this.AcceptButton = _btnOk;
            this.CancelButton = _btnCancel;

            // Обработчики событий
            _btnOk.Click += BtnOk_Click;
            _numOffsetX.ValueChanged += NumOffset_ValueChanged;
            _numOffsetY.ValueChanged += NumOffset_ValueChanged;
            _cmbPresets.SelectedIndexChanged += CmbPresets_SelectedIndexChanged;
        }

        private void BtnOk_Click(object sender, EventArgs e)
        {
            OffsetX = (double)_numOffsetX.Value;
            OffsetY = (double)_numOffsetY.Value;
        }

        private void NumOffset_ValueChanged(object sender, EventArgs e)
        {
            UpdatePreview();
            UpdatePresetSelection();
        }

        private void CmbPresets_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_cmbPresets.SelectedItem != null)
            {
                string presetName = _cmbPresets.SelectedItem.ToString();
                if (_presets.ContainsKey(presetName))
                {
                    var preset = _presets[presetName];
                    _numOffsetX.Value = (decimal)preset.X;
                    _numOffsetY.Value = (decimal)preset.Y;
                    UpdatePreview();
                    UpdateDescription();
                }
            }
        }

        private void UpdatePreview()
        {
            double offsetXmm = (double)_numOffsetX.Value;
            double offsetYmm = (double)_numOffsetY.Value;

            _lblPreviewX.Text = $"({offsetXmm / 1000.0:F2} м)";
            _lblPreviewY.Text = $"({offsetYmm / 1000.0:F2} м)";
        }

        private void UpdateDescription()
        {
            double offsetXmm = (double)_numOffsetX.Value;
            double offsetYmm = (double)_numOffsetY.Value;

            _lblDescription.Text = $"X: {offsetXmm} мм, Y: {offsetYmm} мм  ({offsetXmm / 1000:F1} м × {offsetYmm / 1000:F1} м)";
        }

        private void UpdatePresetSelection()
        {
            double offsetXmm = (double)_numOffsetX.Value;
            double offsetYmm = (double)_numOffsetY.Value;

            // Ищем совпадение с предустановками
            string matchedPreset = "Пользовательский";
            foreach (var preset in _presets)
            {
                if (preset.Key != "Пользовательский" &&
                    Math.Abs(preset.Value.X - offsetXmm) < 1 &&
                    Math.Abs(preset.Value.Y - offsetYmm) < 1)
                {
                    matchedPreset = preset.Key;
                    break;
                }
            }

            // Обновляем выбранную предустановку
            if (_cmbPresets.SelectedItem == null || _cmbPresets.SelectedItem.ToString() != matchedPreset)
            {
                _cmbPresets.SelectedItem = matchedPreset;
                UpdateDescription();
            }
        }
    }
}
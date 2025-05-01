using System;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Autodesk.Revit.Attributes;

namespace LintelsOnTheFloor
{
    public partial class MainWindow : Window
    {
        private readonly Document _doc;
        private readonly UIDocument _uiDoc;

        public MainWindow()
        {
            InitializeComponent();

            if (App.CommandData == null)
            {
                Close();
                return;
            }

            _doc = App.CommandData.Application.ActiveUIDocument.Document;
            _uiDoc = App.CommandData.Application.ActiveUIDocument;

            InitializeControls();
        }

        private void InitializeControls()
        {
            try
            {
                // Загрузка уровней
                cmbLevels.ItemsSource = new FilteredElementCollector(_doc)
                    .OfClass(typeof(Level))
                    .Cast<Level>()
                    .OrderBy(l => l.Elevation)
                    .ToList();

                cmbLevels.DisplayMemberPath = "Name";

                // Загрузка типов перемычек
                cmbLintelTypes.ItemsSource = new FilteredElementCollector(_doc)
                    .OfCategory(BuiltInCategory.OST_StructuralFraming)
                    .WhereElementIsElementType()
                    .Cast<FamilySymbol>()
                    .ToList();

                cmbLintelTypes.DisplayMemberPath = "Name";

                // Установка значений по умолчанию
                txtOffset.Text = "0.05";
                cmbLintelTypes.SelectedIndex = 0;

                // Выбор активного уровня
                var activeLevel = (_doc.ActiveView as View)?.GenLevel;
                if (activeLevel != null)
                {
                    foreach (Level level in cmbLevels.Items)
                    {
                        if (level.Id == activeLevel.Id)
                        {
                            cmbLevels.SelectedItem = level;
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Ошибка инициализации", ex.Message);
            }
        }

        private void BtnCreate_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateInput()) return;

            var level = (Level)cmbLevels.SelectedItem;
            var lintelType = (FamilySymbol)cmbLintelTypes.SelectedItem;
            var offset = double.Parse(txtOffset.Text);

            try
            {
                var doors = GetDoorsOnLevel(level);
                if (doors.Count == 0)
                {
                    TaskDialog.Show("Информация", $"На уровне '{level.Name}' не найдено дверей");
                    return;
                }

                var result = CreateLintels(doors, lintelType, offset);
                TaskDialog.Show("Результат",
                    $"Успешно создано {result.SuccessCount} перемычек из {result.TotalCount}");
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Ошибка", ex.Message);
            }
        }

        private bool ValidateInput()
        {
            if (cmbLevels.SelectedItem == null)
            {
                TaskDialog.Show("Ошибка", "Не выбран уровень");
                return false;
            }

            if (cmbLintelTypes.SelectedItem == null)
            {
                TaskDialog.Show("Ошибка", "Не выбран тип перемычки");
                return false;
            }

            if (!double.TryParse(txtOffset.Text, out double offset) || offset < 0)
            {
                TaskDialog.Show("Ошибка", "Некорректное значение отступа. Введите положительное число.");
                return false;
            }

            return true;
        }

        private IList<FamilyInstance> GetDoorsOnLevel(Level level)
        {
            return new FilteredElementCollector(_doc)
                .OfCategory(BuiltInCategory.OST_Doors)
                .WhereElementIsNotElementType()
                .Cast<FamilyInstance>()
                .Where(d => d.LevelId == level.Id)
                .ToList();
        }

        private (int SuccessCount, int TotalCount) CreateLintels(
            IList<FamilyInstance> doors,
            FamilySymbol lintelType,
            double offset)
        {
            int successCount = 0;

            using (Transaction trans = new Transaction(_doc, "Создание перемычек"))
            {
                trans.Start();

                if (!lintelType.IsActive) lintelType.Activate();

                foreach (var door in doors)
                {
                    if (TryCreateLintel(door, lintelType, offset))
                    {
                        successCount++;
                    }
                }

                trans.Commit();
            }

            return (successCount, doors.Count);
        }

        private bool TryCreateLintel(FamilyInstance door, FamilySymbol lintelType, double offset)
        {
            try
            {
                var bbox = door.get_BoundingBox(null);
                if (bbox == null) return false;

                var level = _doc.GetElement(door.LevelId) as Level;
                if (level == null) return false;

                var hostWall = door.Host as Wall;
                if (hostWall == null) return false;

                var locCurve = hostWall.Location as LocationCurve;
                if (locCurve?.Curve == null) return false;

                var curve = locCurve.Curve;
                var wallDirection = (curve.GetEndPoint(1) - curve.GetEndPoint(0)).Normalize();

                var doorLoc = door.Location as LocationPoint;
                if (doorLoc == null) return false;

                var doorWidth = bbox.Max.X - bbox.Min.X;
                var start = doorLoc.Point - wallDirection * (doorWidth / 2);
                var end = doorLoc.Point + wallDirection * (doorWidth / 2);

                //var lintel = _doc.Create.NewFamilyInstance(
                //    Line.CreateBound(start, end),
                //    lintelType,
                //    hostWall,
                //    level,
                //    Autodesk.Revit.DB.Structure.StructuralType.Beam);

                //var elevParam = lintel.get_Parameter(BuiltInParameter.INSTANCE_ELEVATION_PARAM);
                //if (elevParam?.Set(bbox.Max.Z + offset - level.Elevation) == true)
                //{
                return true;
                //}

                //return false;
            }
            catch
            {
                return false;
            }
        }
    }
}

#region Namespace

using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

using System;
using System.Collections.Generic;
using System.Linq;

#endregion

namespace RevitAddIn2BIMRU.Commands.Parameters
{
    [Transaction(TransactionMode.Manual)]
    public class RemoveDuplicateProjectParameters : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;
            UIDocument uiDoc = uiApp.ActiveUIDocument;
            Document doc = uiDoc.Document;

            try
            {
                // Удаляем дубликаты
                int removedCount = DeleteDuplicateParameters(doc);

                if (removedCount == 0)
                {
                    TaskDialog.Show("Информация", "Дублированные параметры не найдены.");
                }
                else
                {
                    TaskDialog.Show("Готово", $"Удалено {removedCount} дублированных параметров.");
                }

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("Ошибка", ex.Message);
                return Result.Failed;
            }
        }

        public int DeleteDuplicateParameters(Document doc)
        {
            BindingMap bindingMap = doc.ParameterBindings;

            Dictionary<string, Definition> uniqueParams = new Dictionary<string, Definition>();
            List<Definition> toRemove = new List<Definition>();

            // Используем ForwardIterator
            var iterator = bindingMap.ForwardIterator();
            iterator.Reset();

            while (iterator.MoveNext())
            {
                Definition definition = iterator.Key as Definition;
                if (definition == null) continue;

                string paramName = definition.Name;

                if (uniqueParams.ContainsKey(paramName))
                {
                    toRemove.Add(definition);
                }
                else
                {
                    uniqueParams.Add(paramName, definition);
                }
            }

            if (toRemove.Count == 0)
                return 0;

            using (Transaction t = new Transaction(doc, "Удаление дублированных параметров"))
            {
                t.Start();

                foreach (Definition def in toRemove)
                {
                    bindingMap.Remove(def);
                }

                t.Commit();
            }

            return toRemove.Count;
        }
    }
}
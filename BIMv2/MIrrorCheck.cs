using System;
using System.Collections;
using System.Collections.Generic;

using System.Linq;
using System.Windows.Controls;

using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Application = Autodesk.Revit.ApplicationServices.Application;


namespace BIMv2
{
    [Transaction(TransactionMode.Manual)]
    class MIrrorCheck : IExternalCommand
    {
        private Application _app;
        private Document _doc;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uiApp = commandData.Application;
            var uiDoc = uiApp.ActiveUIDocument;

            _app = uiApp.Application;
            _doc = uiDoc.Document;

            uiApp = commandData.Application;
            uiDoc = uiApp.ActiveUIDocument;

            //полуичили все элементы окна и двери
            var allElements = GetAllElements(_doc);

            FilteredElementCollector collector = new FilteredElementCollector(_doc);
            ICollection<Element> windows = collector.OfCategory(BuiltInCategory.OST_Windows)
                                                  .OfClass(typeof(FamilyInstance))
                                                  .Where(e => ((FamilyInstance)e).Host == null)
                                                  .ToList();


            int i = 0;


            foreach (Element elementToModify in windows)
            {
                // Получаем группу, которой принадлежит элемент
                Group group = null;

                var groups = elementToModify.GroupId;

                // Проверяем, находится ли элемент в группе
                Group group1 = elementToModify.GroupId != ElementId.InvalidElementId
                    ? _doc.GetElement(elementToModify.GroupId) as Group
                    : null;

                if (groups != null)
                {
                    // Получаем все экземпляры группы в проекте
                    //ICollection<ElementId> groupInstances = group1.GetAllGroupMemberInstanceIds();


                    Group selectedGroup = group as Group;
                    // Получаем все экземпляры этой группы
                    //ICollection<ElementId> groupInstances = group.GetMemberIds();

                    // Модифицируем исходный элемент и все его копии в экземплярах группы
                    using (Transaction t = new Transaction(_doc, "Modify grouped elements"))
                    {
                        t.Start("1");

                        // 1. Модифицируем исходный элемент
                        //ModifyElement(elementToModify);

                        // Разгруппировать (получаем список элементов, которые были в группе)
                        //ICollection<ElementId> ungroupedElements = group.UngroupMembers();

                        // Войти в режим редактирования группы (для Revit 2021)
                        using (SubTransaction st = new SubTransaction(_doc))
                        {
                            st.Start();

                            // Активировать редактирование группы
                            _doc.Regenerate();
                            selectedGroup.UngroupMembers();
                            ModifyElement(elementToModify);
                            _doc.Regenerate();
                            st.Commit();
                        }

                        // 2. Модифицируем соответствующие элементы в экземплярах группы
                        //foreach (ElementId instanceId in groupInstances)
                        //{
                        //    FamilyInstance groupInstance = _doc.GetElement(instanceId) as FamilyInstance;

                        //    if (groupInstance != null)
                        //    {
                        //        Находим соответствующий элемент в экземпляре группы
                        //        Element correspondingElement = FindCorrespondingElementInGroupInstance(elementToModify, groupInstance);
                        //        if (correspondingElement != null)
                        //        {
                        //            ModifyElement(correspondingElement);
                        //            TaskDialog.Show("Сделано!", "nen");
                        //        }
                        //    }
                        //}

                        t.Commit();
                    }
                }
                else
                {
                    // Элемент не в группе - просто модифицируем его
                    using (Transaction t = new Transaction(_doc, "Modify element"))
                    {
                        t.Start();
                        ModifyElement(elementToModify);
                        t.Commit();
                    }
                }

            }


            //TaskDialog.Show("Сделано!", $"Информация перенесена для {allElements.Count} окон");
            return Result.Succeeded;

        }


        public IList<Element> GetAllElements(Document document)
        {
            // List all Door's
            var collector = new FilteredElementCollector(document)
                .OfCategory(BuiltInCategory.OST_Windows)
                .WhereElementIsNotElementType();

            IList<Element> elements = collector.ToElements();
            return elements;
        }

        private void ModifyElement(Element element)
        {
            // Ваша логика модификации элемента
            FamilyInstance doorFamilyInstance = element as FamilyInstance;

            bool mirFI = doorFamilyInstance.Mirrored;


            if (mirFI == true)
            {


                element.LookupParameter("BIM_Зeркальный").Set(1);

            }

            else
            {
                element.LookupParameter("BIM_Зeркальный").Set(0);
            }
        }

        private Element FindCorrespondingElementInGroupInstance(Element originalElement, FamilyInstance groupInstance)
        {
            
            
                Element member = _doc.GetElement(groupInstance.Id);
                // Здесь нужно определить, как сопоставить оригинальный элемент с элементом в группе
                // Обычно это делается по типу элемента и некоторым уникальным характеристикам
                if (IsCorrespondingElement(originalElement, member))
                {
                    return member;
                }

                return null;
            
        }

        private bool IsCorrespondingElement(Element original, Element candidate)
        {
            // Реализуйте логику сравнения элементов
            // Например, сравнение по типу и некоторым параметрам
            return original.GetTypeId() == candidate.GetTypeId()
                   && original.Name == candidate.Name;
        }
    } 
}
            

                
                

                
                    //var groupID=door.GroupId;

                    //if (groupID != null)
                    //{
                        

                    //    Group selectedGroup = _doc.GetElement(groupID) as Group;

                    //    // Получаем все экземпляры этой группы
                    //    ICollection<ElementId> groupInstances = selectedGroup.GetMemberIds();

                    //    selectedGroup.

                    //    using (SubTransaction st = new SubTransaction(_doc))
                    //    {
                    //        st.Start();

                    //        // Активировать редактирование группы
                    //        //_doc.Regenerate();
                    //        //selectedGroup.un

                    //        // Изменить параметры элемента
                    //        //Parameter param = door.LookupParameter("Комментарии");
                    //        //if (param != null && !param.IsReadOnly)
                    //        //{
                    //        //    param.Set("Изменено через API в Revit 2021");
                    //        //}

                    //        st.Commit();
                    //    }
                    //}
                    //try
                    //{
                    //    FamilyInstance doorFamilyInstance = door as FamilyInstance;

                    //    bool mirFI = doorFamilyInstance.Mirrored;


                    //    if (mirFI == true)
                    //    {
                    //        i++;

                    //        door.LookupParameter("BIM_Зeркальный").Set(1);

                    //    }

                    //    else
                    //    {
                    //        door.LookupParameter("BIM_Зeркальный").Set(0);
                    //    }

                    //}

                    //catch (Exception ex)
                    //{
                    //    message = ex.Message;
                    //    return Result.Failed;
                    //}




                    //}

               

              





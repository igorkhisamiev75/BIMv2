#region Namespace

using System;
using System.Collections.Generic;
using System.IO;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using ExcelDataReader;



#endregion

namespace BIMv2
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CreateNotPlacedRooms : IExternalCommand

    {
        public List<string> RoomNameList = new List<string>();
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)

        {
            string pathToFile = @"C:\Project\B2\2022-08-26_ЛСР_Библиотеки_ТИМ\03_Скрипты\АР\Квартирография\Классификатор помещений (справочно).xlsx";

            // Создаем поток для чтения.
            var stream = File.Open(pathToFile, FileMode.Open, FileAccess.Read);

            // Читатель для файлов с расширением *.xlsx.
            var excelReader = ExcelReaderFactory.CreateOpenXmlReader(stream);
            
            // Читаем, получаем DataSet и работаем с ним как обычно.
            var result = excelReader.AsDataSet();

            var dt = result.Tables[0];

            //var temp=dt.Rows[1][2].ToString();

            var rowsCount = result.Tables[0].Rows.Count;

            for (int i = 1; i < rowsCount; i++)
            {
                RoomNameList.Add(dt.Rows[i][4].ToString());
            }

            // После завершения чтения освобождаем ресурсы.
            excelReader.Close();


            Document doc = commandData.Application.ActiveUIDocument.Document;
            Phase newConstructionPhase = doc.Phases.get_Item(1);

            using (var t = new Transaction(doc))
            {
                t.Start("Create Rooms");


                try
                {
                    // create room using Phase
                   
                    int k = 0;

                    foreach (var roomName in RoomNameList)
                    {
                        Room newScheduleRoom = doc.Create.NewRoom(newConstructionPhase);

                        // set the Room Number and Name
                        string newRoomNumber = "удалить";
                        string newRoomName = roomName;
                        newScheduleRoom.Name = newRoomName;
                        newScheduleRoom.Number = newRoomNumber;
                        
                        //doc.Regenerate();
                        k++;
                    }

                    t.Commit();
                    return Result.Succeeded;

                }



                catch (Exception ex)
                {
                    TaskDialog.Show("Revit", ex.Message);
                    return Result.Failed;
                }

            }

        }


    }
}
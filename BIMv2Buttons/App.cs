using System;
using System.Reflection;
using Autodesk.Revit.UI;
using System.Windows.Media.Imaging;
using BIMv2;
using System.Collections.Generic;
using System.Configuration.Assemblies;
using AW = Autodesk.Windows;
using Autodesk.Windows;

//using CreateFinishWallinRooms;

namespace BIMv2Buttons
{
    public class App : IExternalApplication
    {
        // define a method that will create our tab and button
        static void AddRibbonPanel(UIControlledApplication application)
        {

            // Get dll assembly path
            string thisAssemblyPath = Assembly.GetExecutingAssembly().Location;

            #region BIMv2AI

            //String tabNameB3 = " BIMv2AI ";    // создание вкладки
            //application.CreateRibbonTab(tabNameB3);

            //// Add a new ribbon panel
            //RibbonPanel ribbonPanelbAI1 = application.CreateRibbonPanel(tabNameB3, "Надстройки для лучших дизайнеров"); //подпись под кнопками

            //// Create three push buttons for split button drop down 

            ////попробуем создать еще одну кнопку
            //ribbonPanelbAI1.AddSeparator(); //разделитель между кнопками

            ////попробуем создать еще одну кнопку
            //ribbonPanelbAI1.AddSeparator(); //разделитель между кнопками

            ////попробуем создать еще одну кнопку
            //ribbonPanelbAI1.AddSeparator(); //разделитель между кнопками

           

            //попробуем создать еще одну кнопку
            //ribbonPanelbAI1.AddSeparator(); //разделитель между кнопками

            //// create push button for CurveTotalLength  //namespace и класс
            //PushButtonData pbAI3Data = new PushButtonData("cmdFindNameRoomForFinishElementsRoomsInLink.cs", "Записать номер и имя"
            //    + Environment.NewLine + "в стены, потолоки, полы", thisAssemblyPath, "BIMv2.FindNameRoomForFinishElementsRoomsInLink");

            //PushButton pbAI3 = ribbonPanelbAI1.AddItem(pbAI3Data) as PushButton;
            //pbAI3.ToolTip = "В ADSK_Примечание и Комментарии номер и имя помещений, если пусто";

            //BitmapImage pbAI3Image = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/icons8-roomN-32.png"));
            //pbAI3.LargeImage = pbAI3Image;

            ////попробуем создать еще одну кнопку
            //ribbonPanelbAI1.AddSeparator(); //разделитель между кнопками

          

            ////попробуем создать еще одну кнопку
            //ribbonPanelbAI1.AddSeparator(); //разделитель между кнопками

            #endregion

            #region 2BIM.RU

            String tabNameB2 = "2BIM.RU";    // создание вкладки
            application.CreateRibbonTab(tabNameB2);

            string panelNameRooms = "Rooms";
            string panelNameAR = "AR-Архитектура";
            string panelNameAI = "AI-Дизайн";
            string panelNameElectric = "MEP-Надстройки";
            string panelNameAnnotation = "AN-Аннотации";            
            string panelNameBim = "BIM";
            string panelNameInfo = "Info";

            // Add a new ribbon panel
            Autodesk.Revit.UI.RibbonPanel ribbonPanelArRoom = application.CreateRibbonPanel(tabNameB2, panelNameRooms); //подпись под кнопками
            // Add a new ribbon panel
            Autodesk.Revit.UI.RibbonPanel ribbonPanelAR = application.CreateRibbonPanel(tabNameB2, panelNameAR); //подпись под кнопками
            // Add a new ribbon panel
            Autodesk.Revit.UI.RibbonPanel ribbonPanelAi = application.CreateRibbonPanel(tabNameB2, panelNameAI); //подпись под кнопками
                                                                                                                 // Add a new ribbon panel
            Autodesk.Revit.UI.RibbonPanel ribbonPanelElectric = application.CreateRibbonPanel(tabNameB2, panelNameElectric); //подпись под кнопками
            // Add a new ribbon panel
            Autodesk.Revit.UI.RibbonPanel ribbonPanelAnnotation = application.CreateRibbonPanel(tabNameB2, panelNameAnnotation); //подпись под кнопками
            // Add a new ribbon panel
            Autodesk.Revit.UI.RibbonPanel ribbonPanelBIM = application.CreateRibbonPanel(tabNameB2, panelNameBim); //подпись под кнопками
            // Add a new ribbon panel
            Autodesk.Revit.UI.RibbonPanel ribbonPanelInfo = application.CreateRibbonPanel(tabNameB2, panelNameInfo); //подпись под кнопками



            // Set Image
            BitmapImage TImage = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/icons8-t-16.png", UriKind.RelativeOrAbsolute));
            BitmapImage NImage = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/icons8-circled-n-16.png", UriKind.RelativeOrAbsolute));
            BitmapImage binImage = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/icons8-bin-16.png", UriKind.RelativeOrAbsolute));
            //BitmapImage midleImage = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/icons8-clean2-32.png", UriKind.RelativeOrAbsolute));
            //BitmapImage largeImage = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/icons8-clean2-32.png",UriKind.RelativeOrAbsolute));

            string buttonName3 = "Имена помещений";
            string buttonName4 = "Удалить неразмещенные";
            string buttonName5 = "Прописать тип квартиры";


            // Create three small stacked buttons (16x16 icons)  //A r ui.ribonitems 
            IList<Autodesk.Revit.UI.RibbonItem> stackedButtons = ribbonPanelArRoom.AddStackedItems(
                new PushButtonData(
                    buttonName3,
                    "Имена помещений", //\u200B
                    thisAssemblyPath,
                    "BIMv2.CreateNameSet")
                {
                    Image = NImage, // 16x16 icon
                    ToolTip = "Создать имена помещений по списку с разделителем точка-запятая ;",

                   
                },
                new PushButtonData(
                    buttonName4,
                    "Удалить неразмещенные",
                   thisAssemblyPath,
                    "BIMv2.DelUnplacedRoom")
                {
                    Image = binImage, // 16x16 icon
                    ToolTip = "Удалить лишние помещения"
                },
                new PushButtonData(
                    buttonName5,
                    "Прописать тип квартиры",
                    thisAssemblyPath,
                    "BIMv2.WriteTypeRooms")
                {
                    Image = TImage, // 16x16 icon
                    ToolTip = "Записать тип квартиры"
                }
            );

            // Find Autodes.Windows.RibbonItems

            AW.RibbonItem btnTest3 = GetButton(tabNameB2, panelNameRooms, buttonName3);
            AW.RibbonItem btnTest4 = GetButton(tabNameB2, panelNameRooms, buttonName4);
            AW.RibbonItem btnTest5 = GetButton(tabNameB2, panelNameRooms, buttonName5);

            btnTest3.ShowText = false;
            btnTest4.ShowText = false;
            btnTest5.ShowText = false;

            // create push button for CurveTotalLength  //namespace и класс
            PushButtonData pbAI4Data = new PushButtonData("cmdCheckRooms.cs", "Проверить"
                + Environment.NewLine + "помещения", thisAssemblyPath, "BIMv2.CheckRooms2");

            PushButton pbAI4 = ribbonPanelArRoom.AddItem(pbAI4Data) as PushButton;
            pbAI4.ToolTip = "Прописывает в помещениях верны ли помещения с АР в общий параметр BIM_Error";

            BitmapImage pbAI4Image = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/icons8.png"));
            pbAI4.LargeImage = pbAI4Image;


            //////////////////////////////// AR //////////////////////////////////////////
            ///


            // Set Image
            BitmapImage SImageAR1 = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/icons8-bd-16.png", UriKind.RelativeOrAbsolute));
            BitmapImage SImageAR2 = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/icons8-microsoft-excel-16.png", UriKind.RelativeOrAbsolute));


            string buttonNameAR1 = "Перемычкер";
            string buttonNameAR2 = "ВыгрузкаВExcel";
            string buttonNameAR3 = "ВыгрузкаLegenda";


            // Create three small stacked buttons (16x16 icons)
            IList<Autodesk.Revit.UI.RibbonItem> stackedButtonsAR1 = ribbonPanelAR.AddStackedItems(
                new PushButtonData(
                    buttonNameAR1,
                    "Перемычки", //\u200B
                    thisAssemblyPath,
                    "LintelsOnTheFloor2.LintelsOnTheFloor2")
                {
                    Image = SImageAR1, // 16x16 icon
                    ToolTip = "Создать перемчки и кайфовать",


                },
                new PushButtonData(
                    buttonNameAR2,
                    "Выгрузка в Excel",
                   thisAssemblyPath,
                    "BIMv2.ExportScheduleToExcel")
                {
                    Image = SImageAR2, // 16x16 icon
                    ToolTip = "Открыть спецификацию с картинками для выгрузки"
                },
                new PushButtonData(
                    buttonNameAR3,
                    "ВыгрузкаLegenda",
                    thisAssemblyPath,
                    "BIMv2.RoomExportCommand")
                {
                    Image = SImageAR2, // 16x16 icon
                    ToolTip = "Выгрузить помещения для LEGENDA"
                }
            );

            // Find Autodes.Windows.RibbonItems

            AW.RibbonItem btnNameAR1 = GetButton(tabNameB2, panelNameAR, buttonNameAR1);
            AW.RibbonItem btnNameAR2 = GetButton(tabNameB2, panelNameAR, buttonNameAR2);
            AW.RibbonItem btnNameAR3 = GetButton(tabNameB2, panelNameAR, buttonNameAR3);

            btnNameAR1.ShowText = false;
            btnNameAR2.ShowText = false;
            btnNameAR3.ShowText = false;



            // Create three push buttons for split button drop down 

            // #1 
            PushButtonData pushButtonDataAI2 = new PushButtonData
                ("cmdDoorsInformationTransferFinishDoor.cs", "BIM_Отделка двери", thisAssemblyPath, "BIMv2.DoorsInformationTransferFinishDoor");
            pushButtonDataAI2.LargeImage = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/icons8-doors-32.png", UriKind.RelativeOrAbsolute));

            // #2 
            PushButtonData pushButtonDataAI3 = new PushButtonData
               ("cmdDoorsInformationTransferComplectation.cs", "BIM_Комплектация двери", thisAssemblyPath, "BIMv2.DoorsInformationTransferComplectation");
            pushButtonDataAI3.LargeImage = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/icons8-doorsArm-32.png"));

            // #3 
            PushButtonData pushButtonDataAI4 = new PushButtonData
               ("cmdDDoorsInformationTransferColorFurniture.cs", "BIM_Цвет фурнитура двери", thisAssemblyPath, "BIMv2.DoorsInformationTransferColorFurniture");
            pushButtonDataAI4.LargeImage = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/icons8-backet-32.png"));


            // Make a split button now 
            SplitButtonData splitBtnDataAI2 = new SplitButtonData("SplitButton2", "Split Button2");
            SplitButton splitBtnAI2 = ribbonPanelAR.AddItem(splitBtnDataAI2) as SplitButton;

            splitBtnAI2.AddPushButton(pushButtonDataAI2);
            splitBtnAI2.AddPushButton(pushButtonDataAI3);
            splitBtnAI2.AddPushButton(pushButtonDataAI4);

            ///////////////////////

            // create push button for CurveTotalLength  //namespace и класс
            PushButtonData pbAI2Data = new PushButtonData("cmdFindDoorsMirror.cs", "Зеркально", thisAssemblyPath, "BIMv2.FindDoorsMirror");

            PushButton pbAI2 = ribbonPanelAR.AddItem(pbAI2Data) as PushButton;
            pbAI2.ToolTip = "Найти отзеркаленные двери и окна в свойстве BIM_Зеральный появится галочка";

            BitmapImage pbAI2Image = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/icons8-mir-32.png"));
            pbAI2.LargeImage = pbAI2Image;

            // #1 
            PushButtonData pushButtonDataHl1 = new PushButtonData
                ("cmdMakingHolesForWindows.cs", "Отверстия для окон", thisAssemblyPath, "BIMv2.MakingHolesForWindows");
            pushButtonDataHl1.LargeImage = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/icons8-doors-32.png", UriKind.RelativeOrAbsolute));

            // #2 
            PushButtonData pushButtonDataHl2 = new PushButtonData
               ("cmdMakingHolesForFacadeWindows.cs", "Отверстия для фасада", thisAssemblyPath, "BIMv2.MakingHolesForFacadeWindows");
            pushButtonDataHl2.LargeImage = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/icons8-doorsArm-32.png"));

            // #3 
            PushButtonData pushButtonDataHl3 = new PushButtonData
               ("cmdMakingHolesForFacadeWindowsDoors.cs", "Отверстия окна, двери, фасад", thisAssemblyPath, "BIMv2.MakingHolesForFacadeWindowsDoors");
            pushButtonDataHl3.LargeImage = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/icons8-backet-32.png"));


            // Make a split button now 
            SplitButtonData splitBtnDatHl = new SplitButtonData("SplitButtonHl", "Split ButtonHl");
            SplitButton splitBtnHl = ribbonPanelAR.AddItem(splitBtnDatHl) as SplitButton;

            splitBtnHl.AddPushButton(pushButtonDataHl1);
            splitBtnHl.AddPushButton(pushButtonDataHl2);
            splitBtnHl.AddPushButton(pushButtonDataHl3);



            // #0 
            PushButtonData pushButtonDataAI0 = new PushButtonData
                ("cmdWriteNameRoomInMechanicalEquipment.cs", "В оборудование", thisAssemblyPath, "BIMv2.WriteNameRoomInMechanicalEquipment");
            pushButtonDataAI0.LargeImage = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/icons8.png"));

            // #1 
            PushButtonData pushButtonDataAI1 = new PushButtonData
                ("cmdGWriteNameRoomInFurniture.cs", "В мебель", thisAssemblyPath, "BIMv2.WriteNameRoomInFurniture");
            pushButtonDataAI1.LargeImage = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/icons8-furniture-32.png"));

            // #2 
            PushButtonData pushButtonDataAI2s = new PushButtonData
                ("cmdWriteNameRoomInPlumbingFixtures.cs", "В сантехнику", thisAssemblyPath, "BIMv2.WriteNameRoomInPlumbingFixtures2");
            pushButtonDataAI2s.LargeImage = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/icons8-dush-32.png"));

            // #3 
            PushButtonData pushButtonDataAI3s = new PushButtonData
                ("cmdFindNameRoomForFloor", "В полы", thisAssemblyPath, "BIMv2.FindNameRoomForFloor");
            pushButtonDataAI3s.LargeImage = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/icons8-roomN-32.png"));

            // #4 
            PushButtonData pushButtonDataAI4s = new PushButtonData
                ("cmdFindNameRoomForWall", "В стены", thisAssemblyPath, "BIMv2.FindNameRoomForWall");
            pushButtonDataAI4s.LargeImage = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/icons8-roomN-32.png"));

            // #5 
            PushButtonData pushButtonDataAI5s = new PushButtonData
                ("cmdFindNameRoomForCeiling", "В потолки", thisAssemblyPath, "BIMv2.FindNameRoomForCeiling");
            pushButtonDataAI5s.LargeImage = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/icons8-roomN-32.png"));


            // Make a split button now 
            SplitButtonData splitBtnDataAI = new SplitButtonData("SplitButton", "Split Button");
            SplitButton splitBtnAI = ribbonPanelAi.AddItem(splitBtnDataAI) as SplitButton;

            splitBtnAI.AddPushButton(pushButtonDataAI0);
            splitBtnAI.AddPushButton(pushButtonDataAI1);
            splitBtnAI.AddPushButton(pushButtonDataAI2s);
            splitBtnAI.AddPushButton(pushButtonDataAI3s);
            splitBtnAI.AddPushButton(pushButtonDataAI4s);
            splitBtnAI.AddPushButton(pushButtonDataAI5s);

            // create push button for CurveTotalLength  //namespace и класс
            PushButtonData b1DataB2_2 = new PushButtonData("cmdCreteFinishWalls.cs", "Отделка", thisAssemblyPath, "BIMv2.CreteFinishWalls");

            PushButton pb1B2_2 = ribbonPanelAi.AddItem(b1DataB2_2) as PushButton;
            pb1B2_2.ToolTip = "Создать отделочные стены, потолки и полы в помещении, *потолоки с 23 ревита";

            BitmapImage pb1ImageB2_2 = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/icons8-paint-bucket-32.png"));
            pb1B2_2.LargeImage = pb1ImageB2_2;

            //ribbonPanelAi.AddSeparator();

            // create push button for CurveTotalLength  //namespace и класс
            PushButtonData b1DataB3_7 = new PushButtonData("cmdFinishStairs", "Отделка"
                + Environment.NewLine + "лестницы", thisAssemblyPath, "FinishStairs.FinishingOnTheStairs");

            PushButton pb1B3_7 = ribbonPanelAi.AddItem(b1DataB3_7) as PushButton;
            pb1B3_7.ToolTip = "Сделать отделку на лестнице";

            BitmapImage pb1ImageB3_7 = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/icons8-stairs-32.png"));
            pb1B3_7.LargeImage = pb1ImageB3_7;



            // #1 
            PushButtonData pushButtonData1Count = new PushButtonData("cmdCountFinishWall.cs", "Посчитать отделку - стены", thisAssemblyPath, "BIMv2.CountFinishWall");
            pushButtonData1Count.LargeImage = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/icons8-floor-32.png"));
            // #2 
            PushButtonData pushButtonData2Count = new PushButtonData("cmdCountFinishFloor.cs", "Посчитать отделку - полы", thisAssemblyPath, "BIMv2.CountFinishFloor");
            pushButtonData2Count.LargeImage = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/icons8-floor-32.png"));
            // #3 
            PushButtonData pushButtonData3Count = new PushButtonData("cmdCountFinishCeiling.cs", "Посчитать отделку - потолки", thisAssemblyPath, "BIMv2.CountFinishCeiling");
            pushButtonData3Count.LargeImage = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/icons8-floor-32.png"));

            // Make a split button now 
            SplitButtonData splitBtnDataCount = new SplitButtonData("SplitButton3", "Split Button3");
            SplitButton splitBtnCount = ribbonPanelAi.AddItem(splitBtnDataCount) as SplitButton;

            splitBtnCount.AddPushButton(pushButtonData1Count);
            splitBtnCount.AddPushButton(pushButtonData2Count);
            splitBtnCount.AddPushButton(pushButtonData3Count);

           

            //// create push button for CurveTotalLength  //namespace и класс
            //PushButtonData b21Data = new PushButtonData("cmdWallFinishing", "Посчитать"
            //    + Environment.NewLine + "отделку", thisAssemblyPath, "BIMv2Buttons.WallFinishing2");

            //PushButton pb21 = ribbonPanel.AddItem(b21Data) as PushButton;
            //pb21.ToolTip = "Переписывает отделку в помещения";

            //BitmapImage pb21Image = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/icons8-floor-32.png"));
            //pb21.LargeImage = pb21Image;


            //попробуем создать еще одну кнопку
            //ribbonPanelAi.AddSeparator(); //разделитель между кнопками

            // Create three push buttons for split button drop down 

            // #1 
            PushButtonData pushButtonData1 = new PushButtonData("cmdGroupTypeFloorByNote.cs", "Сгруппировать полы", thisAssemblyPath, "BIMv2Buttons.GroupTypeFloorByNote");
            pushButtonData1.LargeImage = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/icons8-floor-32.png"));

            // #2 
            PushButtonData pushButtonData2 = new PushButtonData("cmdGroupTypeCeilingByNote.cs", "Сгруппировать потолки", thisAssemblyPath, "BIMv2Buttons.GroupTypeCeilingByNote");
            pushButtonData2.LargeImage = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/icons8-floor-32.png"));

            // #3 
            PushButtonData pushButtonData3 = new PushButtonData("cmdGroupTypeWallsByNote.cs", "Сгруппировать стены", thisAssemblyPath, "BIMv2Buttons.GroupTypeWallsByNote");
            pushButtonData3.LargeImage = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/icons8-floor-32.png"));

            // #4 
            PushButtonData pushButtonData4 = new PushButtonData("cmdGroupByMarkEqupmentFurnitureAndWriteToComment.cs", "Сгруппировать оборудование и мебель", thisAssemblyPath, "BIMv2.GroupByMarkEqupmentFurnitureAndWriteToComment");
            pushButtonData4.LargeImage = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/icons8-floor-32.png"));

            // #5 
            PushButtonData pushButtonData5 = new PushButtonData("cmdGroupingLighting.cs", "Сгруппировать светильники", thisAssemblyPath, "BIMv2.GroupingLighting");
            pushButtonData5.LargeImage = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/icons8-floor-32.png"));


            // Make a split button now 
            SplitButtonData splitBtnData = new SplitButtonData("SplitButtonGR", "Split ButtonGR");
            SplitButton splitBtn = ribbonPanelAi.AddItem(splitBtnData) as SplitButton;

            splitBtn.AddPushButton(pushButtonData1);
            splitBtn.AddPushButton(pushButtonData2);
            splitBtn.AddPushButton(pushButtonData3);
            splitBtn.AddPushButton(pushButtonData4);
            splitBtn.AddPushButton(pushButtonData5);


            // create push button for CurveTotalLength  //namespace и класс
            PushButtonData pbAI1Data = new PushButtonData("cmdCreateRailingInRooms.cs", "Создать"
                + Environment.NewLine + "плинтуса в помещении", thisAssemblyPath, "BIMv2.CreateRailingInRooms");

            PushButton pbAI1 = ribbonPanelAi.AddItem(pbAI1Data) as PushButton;
            pbAI1.ToolTip = "Создать плинтус от_Плинтус напольный_2000х60 в выбранном помещении";

            BitmapImage pbAI1Image = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/icons8-rail-3-32.png"));
            pbAI1.LargeImage = pbAI1Image;


            ////попробуем создать еще одну кнопку
            //ribbonPanelAi.AddSeparator(); //разделитель между кнопками

            ////попробуем создать еще одну кнопку
            //ribbonPanelArRoom.AddSeparator(); //разделитель между кнопками

            //// create push button for CurveTotalLength  //namespace и класс
            //PushButtonData b23Data = new PushButtonData("cmdWriteTypeRooms.cs", "Записать"
            //    + Environment.NewLine + "тип квартиры", thisAssemblyPath, "BIMv2.WriteTypeRooms");

            //PushButton pb23 = ribbonPanelArRoom.AddItem(b23Data) as PushButton;
            //pb23.ToolTip = "Записать тип квартиры";

            //BitmapImage pb23Image = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/icons8-typeRoom-32.png"));
            //pb23.LargeImage = pb23Image;

            //попробуем создать еще одну кнопку
            //ribbonPanelArRoom.AddSeparator(); //разделитель между кнопками

            // Create three push buttons for split button drop down 

            ///////////////////// ES

            string buttonES1 = "Посчитать светильники";
            string buttonES2 = "Записать освещенности";
            string buttonES3 = "Расставить светильники";
            string buttonES4 = "Разрез линейного элемента";

            // #1 
            PushButtonData pushButtonDataES1 = new PushButtonData
                ("cmdSumAllLightingFixtureInRoom.cs", buttonES1, thisAssemblyPath, "BIMv2.SumAllLightingFixtureInRoom");
            pushButtonDataES1.LargeImage = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/icons8-sl-32.png"));
            //pushButtonDataES1.ToolTip = "Подсчет количества светильников в помещении и запись в марку ADSK_Количество светильников";

            // #2 
            PushButtonData pushButtonDataES2 = new PushButtonData
               ("cmdNormalIlluminationForLightFix.cs", buttonES2, thisAssemblyPath, "BIMv2.NormalIlluminationForLightFix");
            pushButtonDataES2.LargeImage = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/icons8-nl-32.png"));
            //pushButtonDataES2.ToolTip = "Запись нормируемой освещенности";

            // #3 
            PushButtonData pushButtonDataES3 = new PushButtonData
               ("cmdCreateLightingInRoom.cs", buttonES3, thisAssemblyPath, "BIMv2.CreateLightingInRoom");
            pushButtonDataES3.LargeImage = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/icons8-cl-32.png"));

            // #4 
            PushButtonData pushButtonDataES4 = new PushButtonData
               ("cmdCreatePipeSectionView", buttonES4, thisAssemblyPath, "BIMv2.CreatePipeSectionView");
            pushButtonDataES4.LargeImage = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/icons8-double-arrows32.png"));
            

            // Make a split button now 
            SplitButtonData splitBtnDataES = new SplitButtonData("SplitButtonES", "Split ButtonES");
            SplitButton splitBtnES = ribbonPanelElectric.AddItem(splitBtnDataES) as SplitButton;

            splitBtnES.AddPushButton(pushButtonDataES4);
            splitBtnES.AddPushButton(pushButtonDataES2);
            splitBtnES.AddPushButton(pushButtonDataES3);
            splitBtnES.AddPushButton(pushButtonDataES1);



            //////////////////// AN

            // create push button for CurveTotalLength  //namespace и класс
            PushButtonData b1DataR = new PushButtonData("cmdCreateRoomUnfolds", "Создание"
                + Environment.NewLine + "разверток", thisAssemblyPath, "CreateRoomUnfolds");

            PushButton pb1R = ribbonPanelAnnotation.AddItem(b1DataR) as PushButton;
            b1DataR.ToolTip = "Создать разрезы в помещеини и разместить их на новом листе";

            BitmapImage pb1ImageR = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/icons8-r-32.png"));
            pb1R.LargeImage = pb1ImageR;


            //попробуем создать еще одну кнопку
            //ribbonPanelAnnotation.AddSeparator(); //разделитель между кнопками

            //// create push button for CurveTotalLength  //namespace и класс
            //PushButtonData b1DataB3_1 = new PushButtonData("cmdCreateNewTypeAnnotation", "Создать"
            //    + Environment.NewLine + "флажок", thisAssemblyPath, "BIMv2.CreateNewTypeAnnotation");

            //PushButton pb1B3_1 = ribbonPanelAnnotation.AddItem(b1DataB3_1) as PushButton;
            //pb1B3_1.ToolTip = "Создает и обновляет флажки";

            //BitmapImage pb1ImageB3_1 = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/icons8-flag-32.png"));
            //pb1B3_1.LargeImage = pb1ImageB3_1;

            // create push button for CurveTotalLength  //namespace и класс
            PushButtonData b1DataB3_6 = new PushButtonData("cmdWallSection", "Разрез"
                + Environment.NewLine + "по стене", thisAssemblyPath, "BIMv2.WallSection");

            PushButton pb1B3_6 = ribbonPanelAnnotation.AddItem(b1DataB3_6) as PushButton;
            pb1B3_6.ToolTip = "Создаем разрез по стене";

            BitmapImage pb1ImageB3_6 = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/icons8-double-arrows32.png"));
            pb1B3_6.LargeImage = pb1ImageB3_6;

            PushButtonData b25Data = new PushButtonData("cmdDuplicateSheets.cs", "Копия листа", thisAssemblyPath, "BIMv2.DuplicateSheets");

            PushButton pb25 = ribbonPanelAnnotation.AddItem(b25Data) as PushButton;
            pb25.ToolTip = "Скопировать лист с видами размещенным на нем";

            // create push button for CurveTotalLength  //namespace и класс
            PushButtonData b1DataB2_1 = new PushButtonData("cmdConvertGrids3Dto2D", "Оси 2D/3D"
                + Environment.NewLine + "поменять", thisAssemblyPath, "BIMv2.ConvertGrids3Dto2D");

            PushButton pb1B2_1 = ribbonPanelAnnotation.AddItem(b1DataB2_1) as PushButton;
            pb1B2_1.ToolTip = "Перевести оси в 2D либо 3D";

            BitmapImage pb1ImageB2_1 = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/icons8-2d-16.png"));
            pb1B2_1.LargeImage = pb1ImageB2_1;

            BitmapImage pb25Image = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/icons8-sheets-32.png"));
            pb25.LargeImage = pb25Image;

            string buttonName33 = "Рабочие наборы";
            string buttonName43 = "3D связей";
            string buttonName53 = "Коллизии";


            // Create three small stacked buttons (16x16 icons)
            IList<Autodesk.Revit.UI.RibbonItem> stackedButtonsBIM = ribbonPanelBIM.AddStackedItems(
                new PushButtonData(
                    buttonName33,
                    "Рабочие наборы", //\u200B
                    thisAssemblyPath,
                    "BIMv2.CreateWorksets")
                {
                    Image = NImage, // 16x16 icon
                    ToolTip = "Создать рабочие наборы в файле",


                },
                new PushButtonData(
                    buttonName43,
                    "3D связей",
                   thisAssemblyPath,
                    "BIMv2.CreateViewByLinkedFiles")
                {
                    Image = binImage, // 16x16 icon
                    ToolTip = "Создать 3D виды для связанных файлов"
                },
                new PushButtonData(
                    buttonName53,
                    "Создать коллизии",
                    thisAssemblyPath,
                    "BIMv2.CreateViewForCollisions")
                {
                    Image = TImage, // 16x16 icon
                    ToolTip = "Создает 3D виды коллизий по отчету на диске С, имя файла должно быть BIMv2_ClashView.xml"
                }
            );

            // Find Autodes.Windows.RibbonItems

            AW.RibbonItem btnTest33 = GetButton(tabNameB2, panelNameBim, buttonName33);
            AW.RibbonItem btnTest43 = GetButton(tabNameB2, panelNameBim, buttonName43);
            AW.RibbonItem btnTest53 = GetButton(tabNameB2, panelNameBim, buttonName53);

            btnTest33.ShowText = false;
            btnTest43.ShowText = false;
            btnTest53.ShowText = false;

            PushButtonData b9Data = new PushButtonData("cmdDeletedFilter", "Удалить"+ Environment.NewLine + "фильтры", thisAssemblyPath, "BIMv2.DeletedFilter");

            PushButton pb9 = ribbonPanelBIM.AddItem(b9Data) as PushButton;
            pb9.ToolTip = "Удалить фильтры";

            BitmapImage pb9Image = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/icons8-clean-32.png"));
            pb9.LargeImage = pb9Image;

            PushButtonData b11Data = new PushButtonData("cmdClearProjectBrowser", "Удалить всё", thisAssemblyPath, "BIMv2.ClearProjectBrowser");

            PushButton pb11 = ribbonPanelBIM.AddItem(b11Data) as PushButton;
            pb11.ToolTip = "Удалить всё кроме активного вида";

            BitmapImage pb11Image = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/icons8-clean3-32.png"));
            pb11.LargeImage = pb11Image;

            //PushButtonData b17Data = new PushButtonData("cmdCreateWorksets", "Рабочие наборы", thisAssemblyPath, "BIMv2.CreateWorksets");

            //PushButton pb17 = ribbonPanelBIM.AddItem(b17Data) as PushButton;
            //pb17.ToolTip = "Создание рабочих наборов по списку";

            //BitmapImage pb17Image = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/icons8-ws-32.png"));
            //pb17.LargeImage = pb17Image;


            // Create three push buttons for split button drop down 

            // #1 
            PushButtonData pushButtonBIM = new PushButtonData("cmdCreateModelTextRoomNameInRoom.cs", "Помещения в 3D", thisAssemblyPath, "BIMv2.CreateModelTextRoomNameInRoom");
            pushButtonBIM.LargeImage = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/icons8-sign-32.png"));
            // #2 
            PushButtonData pushButtonBIM2 = new PushButtonData("cmdBIMv2.CreateFromDWG.cs", "Элементы по DWG", thisAssemblyPath, "BIMv2.CreateFromDWG");
            pushButtonBIM2.LargeImage = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/icons8-sign-32.png"));
            // #3 
            PushButtonData pushButtonBIM3 = new PushButtonData("cmdCreateFromDWGPipe.cs", "Трубы по DWG", thisAssemblyPath, "BIMv2.CreateFromDWGPipe");
            pushButtonBIM3.LargeImage = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/icons8-sign-32.png"));

            // #3 
            PushButtonData pushButtonBIM4 = new PushButtonData("cmdCreateClashElements.cs", "Точки из Navisworks", thisAssemblyPath, "BIMv2.CreateClashElements");
            pushButtonBIM4.LargeImage = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/icons8-sign-32.png"));




            // Make a split button now 
            SplitButtonData splitBtnBIMCount = new SplitButtonData("SplitButtonBIM", "Split ButtonBIM");
            SplitButton splitBtnBIM = ribbonPanelBIM.AddItem(splitBtnBIMCount) as SplitButton;

            splitBtnBIM.AddPushButton(pushButtonBIM);
            splitBtnBIM.AddPushButton(pushButtonBIM2);
            splitBtnBIM.AddPushButton(pushButtonBIM3);
            splitBtnBIM.AddPushButton(pushButtonBIM4);


            //PushButtonData b26Data = new PushButtonData("cmdCreateViewByLinkedFiles.cs", "3D связей", thisAssemblyPath, "BIMv2.CreateViewByLinkedFiles");

            //PushButton pb26 = ribbonPanelBIM.AddItem(b26Data) as PushButton;
            //pb26.ToolTip = "Создать 3D виды для связанных файлов";

            //BitmapImage pb26Image = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/icons8-3d-modeling-32.png"));
            //pb26.LargeImage = pb26Image;

            //PushButtonData b28Data = new PushButtonData("cmdCreateViewForCollisions.cs", "Коллизии", thisAssemblyPath, "BIMv2.CreateViewForCollisions");

            //PushButton pb28 = ribbonPanelBIM.AddItem(b28Data) as PushButton;
            //pb28.ToolTip = "Создает инфу по отчету на диске С, имя файла должно быть BIMv2_ClashView.xml";

            //BitmapImage pb28Image = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/icons8-bi-32.png"));
            //pb28.LargeImage = pb28Image;


            //PushButtonData bHData5 = new PushButtonData("cmdHelpBIMYouTube", "Подпишись на"
            //  + Environment.NewLine + "YouTube", thisAssemblyPath, "BIMv2.HelpBIMYouTube");

            //PushButton pbH5 = ribbonPanelInfo.AddItem(bHData5) as PushButton;
            //pbH5.ToolTip = "Подписываемся, ставим лайки, комментируем";

            //BitmapImage pbHImage5 = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/icons8-youtube-32.png"));
            //pbH5.LargeImage = pbHImage5;

            PushButtonData bHData2 = new PushButtonData("cmdHelpBIMBlog", "Blog" + Environment.NewLine + "2bim.ru", thisAssemblyPath, "BIMv2.HelpBIMBlog");

            PushButton pbH2 = ribbonPanelInfo.AddItem(bHData2) as PushButton;
            pbH2.ToolTip = "Подписываемся, ставим лайки, комментируем";

            BitmapImage pbHImage2 = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/blog2bim.png"));
            pbH2.LargeImage = pbHImage2;

            PushButtonData bHData1 = new PushButtonData("cmdHelpBIM", "На кофе"
               + Environment.NewLine + "программисту", thisAssemblyPath, "BIMv2.HelpBIMTelegram");

            PushButton pbH1 = ribbonPanelInfo.AddItem(bHData1) as PushButton;
            pbH1.ToolTip = "Подписываемся, ставим лайки, комментируем";

            BitmapImage pbHImage = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/blog2bim.png"));
            pbH1.LargeImage = pbHImage;




            // Add a new ribbon panel
            //RibbonPanel ribbonPanelb2 = application.CreateRibbonPanel(tabNameB2, "Архитектурные надстройки"); //подпись под кнопками







            ////попробуем создать еще одну кнопку
            //ribbonPanelb2.AddSeparator(); //разделитель между кнопками

            //// create push button for CurveTotalLength  //namespace и класс
            //PushButtonData b1DataB3_2 = new PushButtonData("cmdCDoorsInformationTransfer", "Заполнить"
            //    + Environment.NewLine + "двери", thisAssemblyPath, "BIMv2.DoorsInformationTransfer");

            //PushButton pb1B3_2 = ribbonPanelb2.AddItem(b1DataB3_2) as PushButton;
            //pb1B3_2.ToolTip = "Заполняем дверь";

            //BitmapImage pb1ImageB3_2 = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/icons8-door-32.png"));
            //pb1B3_2.LargeImage = pb1ImageB3_2;

            ////попробуем создать еще одну кнопку
            //ribbonPanelb2.AddSeparator(); //разделитель между кнопками



            //попробуем создать еще одну кнопку
            //ribbonPanelb2.AddSeparator(); //разделитель между кнопками



            ////попробуем создать еще одну кнопку
            //ribbonPanelb2.AddSeparator(); //разделитель между кнопками



            //// Add a new ribbon panel
            //RibbonPanel ribbonPanelb3 = application.CreateRibbonPanel(tabNameB2, "Надстройка для ПТО"); //подпись под кнопками

            //// create push button for CurveTotalLength  //namespace и класс
            //PushButtonData pto1Data = new PushButtonData("cmdGroupTypeFloorForBOP.cs", "Подчет перекрытий"
            //    + Environment.NewLine + "для ВОРовиков", thisAssemblyPath, "BIMv2.GroupTypeFloorForBOP");

            //PushButton pbPTO1 = ribbonPanelb3.AddItem(pto1Data) as PushButton;
            //pbPTO1.ToolTip = "Магическим образом не понятным никому, кроме Игоря, объединяет и группирует перекрытия";

            //BitmapImage pbPTO1Image = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/icons8-builder-32.png"));
            //pbPTO1.LargeImage = pbPTO1Image;

            ////попробуем создать еще одну кнопку
            //ribbonPanelb2.AddSeparator(); //разделитель между кнопками




            #endregion

            #region BIMv2

            //String tabName = "BIMv2";    // создание вкладки
            //application.CreateRibbonTab(tabName);

            //// Add a new ribbon panel
            //RibbonPanel ribbonPanel = application.CreateRibbonPanel(tabName, "Надстройки для архитектора"); //подпись под кнопками

            //// create push button for CurveTotalLength  //namespace и класс
            //PushButtonData b1Data = new PushButtonData("cmdWriteNameRoomInPlumbingFixtures", "Помещения" 
            //    + Environment.NewLine + "в элементы", thisAssemblyPath, "BIMv2.WriteNameRoomInPlumbingFixtures");

            //PushButton pb1 = ribbonPanel.AddItem(b1Data) as PushButton;
            //pb1.ToolTip = "Переписывает номера и имена в ADSK_Зона для сантехники, обобщенных моделей, спец.оборудование, мебель";

            //BitmapImage pb1Image = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/transferNameRoom.png"));
            //pb1.LargeImage = pb1Image;


            ////попробуем создать еще одну кнопку
            //ribbonPanel.AddSeparator(); //разделитель между кнопками

            //// create push button for   //namespace и класс
            //PushButtonData b1lData = new PushButtonData("cmdFindNameRoomForLighting", "Помещения"
            //    + Environment.NewLine + "в светильники", thisAssemblyPath, "BIMv2.FindNameRoomForLighting");

            //PushButton pb1l = ribbonPanel.AddItem(b1lData) as PushButton;
            //pb1l.ToolTip = "Переписывает номера в светильники";

            //BitmapImage pb1lImage = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/transferNameRoom.png"));
            //pb1l.LargeImage = pb1lImage;


            //попробуем создать еще одну кнопку
            //ribbonPanel.AddSeparator(); //разделитель между кнопками

            // Create three push buttons for split button drop down 



            //create push button for CurveTotalLength  //namespace и класс
            // PushButtonData b11Data = new PushButtonData("cmdCreateNumberRoomForFloors", "Записать"
            //    + Environment.NewLine + "помещения в полы", thisAssemblyPath, "BIMv2.CreateNumberRoomForFloors");

            //PushButton pb11 = ribbonPanel.AddItem(b11Data) as PushButton;
            // pb11.ToolTip = "Записать помещения в перекрытия";

            // BitmapImage pb11Image = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/icons8-floor-32.png"));
            // pb11.LargeImage = pb11Image;

            //12 группировка полов

            ////попробуем создать еще одну кнопку
            //ribbonPanel.AddSeparator(); //разделитель между кнопками

            //// create push button for CurveTotalLength  //namespace и класс
            //PushButtonData b18Data = new PushButtonData("GroupTypeWallsByNote", "Сгруппировать"
            //    + Environment.NewLine + "стены", thisAssemblyPath, "BIMv2Buttons.GroupTypeWallsByNote");

            //PushButton pb18 = ribbonPanel.AddItem(b18Data) as PushButton;
            //pb18.ToolTip = "Группировка полов и запись в комментарии";

            //BitmapImage pb18Image = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/icons8-wall-32.png"));
            //pb18.LargeImage = pb18Image;

            //12 группировка полов

            //попробуем создать еще одну кнопку
            //ribbonPanel.AddSeparator(); //разделитель между кнопками

            // create push button for CurveTotalLength  //namespace и класс
            PushButtonData b12Data = new PushButtonData("GroupTypeFloorAndWriteToComment.cs", "Сгруппировать полы", thisAssemblyPath, "BIMv2.GroupTypeFloorAndWriteToComment");

            //PushButton pb12 = ribbonPanel.AddItem(b12Data) as PushButton;
            //pb12.ToolTip = "Группировка полов и запись в комментарии";

            b12Data.LargeImage = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/icons8-floor-32.png"));
            b12Data.ToolTip= "Группировка полов и запись в комментарии";

            //pb12.LargeImage = pb12Image;

            //13 группировка потолков

            //попробуем создать еще одну кнопку
            //ribbonPanel.AddSeparator(); //разделитель между кнопками

            // create push button for CurveTotalLength  //namespace и класс
            PushButtonData b13Data = new PushButtonData("GroupTypeCeilingAndWriteToComment.cs", "Сгруппировать потолки", thisAssemblyPath, "BIMv2.GroupTypeCeilingAndWriteToComment");

            //PushButton pb13 = ribbonPanel.AddItem(b13Data) as PushButton;
            //pb13.ToolTip = "Группировка потолков и запись в комментарии";

            b13Data.LargeImage = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/icons8-ceiling-32.png"));
            b13Data.ToolTip= "Группировка потолков и запись в комментарии";
            //pb13.LargeImage = pb13Image;

            //14 группировка стен

            //попробуем создать еще одну кнопку
            //ribbonPanel.AddSeparator(); //разделитель между кнопками
            
            // create push button for CurveTotalLength  //namespace и класс
            PushButtonData b14Data = new PushButtonData("cmdGroupTypeWallsAndWriteToComment.cs", "Сгруппировать стены", thisAssemblyPath, "BIMv2.GroupTypeWallsAndWriteToComment");

            //PushButton pb14 = ribbonPanel.AddItem(b14Data) as PushButton;
            //pb14.ToolTip = "Группировка стен и запись в комментарии";

            b14Data.LargeImage = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/icons8-wall-32.png"));
            b14Data.ToolTip= "Группировка стен и запись в комментарии";
            //pb14.LargeImage = pb14Image;

            //15 группировка профилей стен

            //попробуем создать еще одну кнопку
            //ribbonPanel.AddSeparator(); //разделитель между кнопками
            
            // create push button for CurveTotalLength  //namespace и класс
            PushButtonData b15Data = new PushButtonData("cmdGroupTypeWallSweepsAndWriteToComment.cs", "Сгруппировать профили стен", thisAssemblyPath, "BIMv2.GroupTypeWallSweepsAndWriteToComment");

            //PushButton pb15 = ribbonPanel.AddItem(b15Data) as PushButton;
            //pb15.ToolTip = "Группировка профилей и запись в комментарии";

            b15Data.LargeImage = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/icons8-dosk-32.png"));
            b15Data.ToolTip= "Группировка профилей и запись в комментарии";
            //pb15.LargeImage = pb15Image;


            //// Make a split button now 
            //SplitButtonData splitBtnData2 = new SplitButtonData("SplitButton2", "Split Button2");
            //SplitButton splitBtn2 = ribbonPanel.AddItem(splitBtnData2) as SplitButton;

            //splitBtn2.AddPushButton(b12Data);
            //splitBtn2.AddPushButton(b13Data);
            //splitBtn2.AddPushButton(b14Data);
            //splitBtn2.AddPushButton(b15Data);

            //попробуем создать еще одну кнопку
            //ribbonPanel.AddSeparator(); //разделитель между кнопками
            

            //// create push button for CurveTotalLength  //namespace и класс
            //PushButtonData b3Data = new PushButtonData("cmdCreateModelText.cs", "Создать"
            //    + Environment.NewLine + "текст модели", thisAssemblyPath, "BIMv2.CreateModelText");

            //PushButton pb3 = ribbonPanel.AddItem(b3Data) as PushButton;
            //pb3.ToolTip = "Создать текст модели на основе .ТХТ файла с диска С";

            //BitmapImage pb3Image = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/creteModelText.png"));
            //pb3.LargeImage = pb3Image;


            //// Add a new ribbon panel
            //RibbonPanel ribbonPanel2 = application.CreateRibbonPanel(tabName, "BIM стандарт и помощь"); //подпись под кнопками

            //// create push button for CurveTotalLength  //namespace и класс
            //PushButtonData b4Data = new PushButtonData("cmdBIMStandart.cs", "Почитать"
            //    + Environment.NewLine + "BIM стандарт", thisAssemblyPath, "BIMv2.BIMStandart");

            //PushButton pb4 = ribbonPanel2.AddItem(b4Data) as PushButton;
            //pb4.ToolTip = "Открой хотя бы раз";

            //BitmapImage pb4Image = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/icons8-book-32.png"));
            //pb4.LargeImage = pb4Image;

            //ribbonPanel2.AddSeparator(); //разделитель между кнопками

            //// create push button for CurveTotalLength  //namespace и класс
            //PushButtonData b5Data = new PushButtonData("cmdHelpBIM.cs", "Группа"
            //    + Environment.NewLine + "МРГТ Telegram", thisAssemblyPath, "BIMv2.HelpBIM");

            //PushButton pb5 = ribbonPanel2.AddItem(b5Data) as PushButton;
            //pb5.ToolTip = "Можно написать, но лучше поискать самим в Google";

            //BitmapImage pb5Image = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/icons8-whatsapp-32.png"));
            //pb5.LargeImage = pb5Image;



            //// Add a new ribbon panel
            //RibbonPanel ribbonPanel3 = application.CreateRibbonPanel(tabName, "Надстройки для Миши и Антона"); //подпись под кнопками

            //// create push button for CurveTotalLength  //namespace и класс
            //PushButtonData b2Data = new PushButtonData("cmdSumAllLightingFixtureInRoom", "Посчитать"
            //    + Environment.NewLine + "светильники", thisAssemblyPath, "BIMv2.SumAllLightingFixtureInRoom");

            //PushButton pb2 = ribbonPanel3.AddItem(b2Data) as PushButton;
            //pb2.ToolTip = "Подсчет количества светильников в помещении и запись в марку ADSK_Количество светильников";

            //BitmapImage pb2Image = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/countLampInRoom.png"));
            //pb2.LargeImage = pb2Image;

            ////попробуем создать еще одну кнопку
            //ribbonPanel3.AddSeparator(); //разделитель между кнопками

            ////16

            //// create push button for CurveTotalLength  //namespace и класс
            //PushButtonData b16Data = new PushButtonData("cmdNormalIlluminationForLightFix.cs", "Записать норм."
            //    + Environment.NewLine + "освещенность", thisAssemblyPath, "BIMv2.NormalIlluminationForLightFix");

            //PushButton pb16 = ribbonPanel3.AddItem(b16Data) as PushButton;
            //pb16.ToolTip = "Запись нормируемой освещенности";

            //BitmapImage pb16Image = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/countLampInRoom2.png"));
            //pb16.LargeImage = pb16Image;

            ////попробуем создать еще одну кнопку
            //ribbonPanel3.AddSeparator(); //разделитель между кнопками

            //// create push button for CurveTotalLength  //namespace и класс
            //PushButtonData b6Data = new PushButtonData("cmdCreateLightingInRoom", "Расставить"
            //    + Environment.NewLine + "светильники", thisAssemblyPath, "BIMv2.CreateLightingInRoom");

            //PushButton pb6 = ribbonPanel3.AddItem(b6Data) as PushButton;
            //pb6.ToolTip = "Размещает светильники в помещениях";

            //BitmapImage pb6Image = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/icons8-oil-lamp-32.png"));
            //pb6.LargeImage = pb6Image;

            ////попробуем создать еще одну кнопку
            //ribbonPanel3.AddSeparator(); //разделитель между кнопками

            //// create push button for CurveTotalLength  //namespace и класс
            //PushButtonData b8Data = new PushButtonData("cmdPowerFactorElectrical.cs", "Посчитать"
            //    + Environment.NewLine + "cos фи (φ)", thisAssemblyPath, "BIMv2.PowerFactorElectrical");

            //PushButton pb8 = ribbonPanel3.AddItem(b8Data) as PushButton;
            //pb8.ToolTip = "Расчет cos фи (φ)";

            //BitmapImage pb8Image = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/icons8-cos-32.png"));
            //pb8.LargeImage = pb8Image;

            ////попробуем создать еще одну кнопку
            //ribbonPanel3.AddSeparator(); //разделитель между кнопками

            //// create push button for CurveTotalLength  //namespace и класс
            //PushButtonData b8DataLV = new PushButtonData("cmdCollisionsWithWall.cs", "Прописать"
            //    + Environment.NewLine + "стены в отверстия", thisAssemblyPath, "BIMv2.CollisionsWithWall");

            //PushButton pb8LV = ribbonPanel3.AddItem(b8DataLV) as PushButton;
            //pb8LV.ToolTip = "Прописывает тип стены в ADSK_Примечание";

            //BitmapImage pb8ImageLV = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/icons8.png"));
            //pb8LV.LargeImage = pb8ImageLV;


            // Add a new ribbon panel
            //RibbonPanel ribbonPanel4 = application.CreateRibbonPanel(tabName, "Надстройки для всех"); //подпись под кнопками

            // create push button for CurveTotalLength  //namespace и класс
            //PushButtonData b7Data = new PushButtonData("cmdCreateAnyElementsInRooms", "Расставить"
            //    + Environment.NewLine + "элементы в помещениях", thisAssemblyPath, "BIMv2.CreateAnyElementsInRooms");

            //PushButtonData b7Data = new PushButtonData("cmdCreateElementsInRooms", "Расставить"
            //    + Environment.NewLine + "элементы в помещениях", thisAssemblyPath, "BIMv2.CreateElementsInRooms");

            //PushButton pb7 = ribbonPanel4.AddItem(b7Data) as PushButton;
            //pb7.ToolTip = "Расставить выбранное семейство в помещениях";

            //BitmapImage pb7Image = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/icons8-crane-machine-32.png"));
            //pb7.LargeImage = pb7Image;



            ////попробуем создать еще одну кнопку
            //ribbonPanel4.AddSeparator(); //разделитель между кнопками

            


            ////попробуем создать еще одну кнопку
            //ribbonPanel4.AddSeparator(); //разделитель между кнопками


            //PushButtonData b10Data = new PushButtonData("cmdDeletedUnFilter", "Удалить"
            //                            + Environment.NewLine + "неиспользуемые фильтры", thisAssemblyPath, "BIMv2.DeletedUnFilter");

            //PushButton pb10 = ribbonPanel4.AddItem(b10Data) as PushButton;
            //pb10.ToolTip = "Удалить только неиспользуемые фильтры";

            //BitmapImage pb10Image = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/icons8-clean2-32.png"));
            //pb10.LargeImage = pb10Image;

            ////попробуем создать еще одну кнопку
            //ribbonPanel4.AddSeparator(); //разделитель между кнопками

           

            ////попробуем создать еще одну кнопку
            //ribbonPanel4.AddSeparator(); //разделитель между кнопками

            

            ////попробуем создать еще одну кнопку
            //ribbonPanel4.AddSeparator(); //разделитель между кнопками

            //PushButtonData b20Data = new PushButtonData("cmdCreateModelTextRoomNameInRoom.cs", "Подписать"
            //    + Environment.NewLine + "помещения в 3D", thisAssemblyPath, "BIMv2.CreateModelTextRoomNameInRoom");

            //PushButton pb20 = ribbonPanel4.AddItem(b20Data) as PushButton;
            //pb20.ToolTip = "Подписать помещения с помощью семейства Для подписи помещений.rfa";

            //BitmapImage pb20Image = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/icons8-sign-32.png"));
            //pb20.LargeImage = pb20Image;



            ////попробуем создать еще одну кнопку
            ////ribbonPanel4.AddSeparator(); //разделитель между кнопками

            ////попробуем создать еще одну кнопку
            //ribbonPanel4.AddSeparator(); //разделитель между кнопками

           

            ////попробуем создать еще одну кнопку
            //ribbonPanel4.AddSeparator(); //разделитель между кнопками



           

            //попробуем создать еще одну кнопку
            //ribbonPanel4.AddSeparator(); //разделитель между кнопками

           

            //////попробуем создать еще одну кнопку
            ////ribbonPanel4.AddSeparator(); //разделитель между кнопками

            ////PushButtonData b27Data = new PushButtonData("cmdFindDuctFiting.cs", "Врезки и "
            ////   + Environment.NewLine + "решетки", thisAssemblyPath, "BIMv2.FindDuctFiting");

            ////PushButton pb27 = ribbonPanel4.AddItem(b27Data) as PushButton;
            ////pb27.ToolTip = "Заполнить инфу в комментарии";

            ////BitmapImage pb27Image = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/icons8-hvac-32.png"));
            ////pb27.LargeImage = pb27Image;

            ////попробуем создать еще одну кнопку
            //ribbonPanel4.AddSeparator(); //разделитель между кнопками

            

            ////попробуем создать еще одну кнопку
            //ribbonPanel4.AddSeparator(); //разделитель между кнопками



            // Create three push buttons for split button drop down 

            //// #1 
            //PushButtonData ribbonItemData1 = new PushButtonData("ConvertGrids3Dto2D", "Оси 2D/3D", thisAssemblyPath, "BIMv2Buttons.ConvertGrids3Dto2D");
            //ribbonItemData1.Image = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/icons8-2d-16.png"));

            //// #2 
            //PushButtonData ribbonItemData2 = new PushButtonData("ConvertGrids3Dto2D2", "BIM-Стандарт", thisAssemblyPath, "BIMv2.BIMStandart");
            //ribbonItemData2.Image = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/icons8-book-32.png"));

            //// #3 
            //PushButtonData ribbonItemData3 = new PushButtonData("TelegramGroup", "Telegram", thisAssemblyPath, "BIMv2.HelpBIM");
            //ribbonItemData3.Image = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/icons8-whatsapp-32.png"));

            //// Create a ribbon panel
            //RibbonPanel m_projectPanel = application.CreateRibbonPanel(tabName, "Разное");

            //// Add the buttons to the panel
            //List<RibbonItem> projectButtons = new List<RibbonItem>();
            //projectButtons.AddRange(m_projectPanel.AddStackedItems(ribbonItemData1, ribbonItemData2, ribbonItemData3));

            //m_projectPanel.AddSeparator();

            #endregion

//            #region BIMv2Links

//            String tabNameLinks = "BIMv2Ссылки";    // создание вкладки
//            application.CreateRibbonTab(tabNameLinks);

//            // Add a new ribbon panel
//            RibbonPanel ribbonPanelLinks1 = application.CreateRibbonPanel(tabNameLinks, "Полезные ссылки: Мой YouTube, Telegram, Blog, Boosty, Donationalerts"); //подпись под кнопками

//            PushButtonData bHData1 = new PushButtonData("cmdHelpBIM", "Группа в"
//               + Environment.NewLine + "Телеграм", thisAssemblyPath, "BIMv2.HelpBIMTelegram");

//            PushButton pbH1 = ribbonPanelLinks1.AddItem(bHData1) as PushButton;
//            pbH1.ToolTip = "Подписываемся, ставим лайки, комментируем";

//            BitmapImage pbHImage = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/icons8-whatsapp-32.png"));
//            pbH1.LargeImage = pbHImage;

//            //попробуем создать еще одну кнопку
//            ribbonPanelLinks1.AddSeparator(); //разделитель между кнопками

//           

//            //попробуем создать еще одну кнопку
//            ribbonPanelLinks1.AddSeparator(); //разделитель между кнопками

//            PushButtonData bHData3 = new PushButtonData("cmdHelpBIMBoosty", "Поддержать"
//              + Environment.NewLine + "Boosty", thisAssemblyPath, "BIMv2.HelpBIMBoosty");

//            PushButton pbH3 = ribbonPanelLinks1.AddItem(bHData3) as PushButton;
//            pbH3.ToolTip = "Подписываемся, ставим лайки, комментируем";

//            BitmapImage pbHImage3 = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/boosty.png"));
//            pbH3.LargeImage = pbHImage3;

//            //попробуем создать еще одну кнопку
//            ribbonPanelLinks1.AddSeparator(); //разделитель между кнопками


//            PushButtonData bHData4 = new PushButtonData("cmdHelpBIMDA", "Поддержать"
//              + Environment.NewLine + "donationalerts", thisAssemblyPath, "BIMv2.HelpBIMDA");

//            PushButton pbH4 = ribbonPanelLinks1.AddItem(bHData4) as PushButton;
//            pbH4.ToolTip = "Подписываемся, ставим лайки, комментируем";

//            BitmapImage pbHImage4 = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/da.png"));
//            pbH4.LargeImage = pbHImage4;

//            //попробуем создать еще одну кнопку
//            ribbonPanelLinks1.AddSeparator(); //разделитель между кнопками

//            

//            //попробуем создать еще одну кнопку
//            ribbonPanelLinks1.AddSeparator(); //разделитель между кнопками



//#endregion


        }

        public Result OnShutdown(UIControlledApplication application)
        {
            // do nothing
            return Result.Succeeded;
        }

        public Result OnStartup(UIControlledApplication application)
        {
            // call our method that will load up our toolbar
            AddRibbonPanel(application);
            return Result.Succeeded;
        }

        public System.Windows.Media.ImageSource LoadImage(string imageName)
        {
            try
            {
                // Load from embedded resources
                System.IO.Stream stream = this.GetType().Assembly.GetManifestResourceStream(
                    "MyNamespace.Resources." + imageName);

                return System.Windows.Media.Imaging.BitmapFrame.Create(stream);
            }
            catch
            {
                return null;
            }
        }

        public static AW.RibbonItem GetButton(string tabName, string panelName, string itemName)
        {
            AW.RibbonControl ribbon = AW.ComponentManager.Ribbon;
            foreach (AW.RibbonTab tab in ribbon.Tabs)
            {
                if (tab.Name == tabName)
                {
                    foreach (AW.RibbonPanel panel in tab.Panels)
                    {
                        if (panel.Source.Title == panelName)
                        {
                            return panel.FindItem("CustomCtrl_%CustomCtrl_%"
                              + tabName + "%" + panelName + "%" + itemName,
                              true) as AW.RibbonItem;
                        }
                    }
                }
            }
            return null;
        }

    }
}

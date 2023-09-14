using System;
using System.Reflection;
using Autodesk.Revit.UI;
using System.Windows.Media.Imaging;
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

            #region BIMv2AR

            String tabNameB2 = "BIMv2AR";    // создание вкладки
            application.CreateRibbonTab(tabNameB2);

            // Add a new ribbon panel
            RibbonPanel ribbonPanelb2 = application.CreateRibbonPanel(tabNameB2, "Архитектурные надстройки"); //подпись под кнопками

            // create push button for CurveTotalLength  //namespace и класс
            PushButtonData b22Data = new PushButtonData("cmdCreateNotPlacedRooms.cs", "Создать"
                + Environment.NewLine + "имена помещений", thisAssemblyPath, "BIMv2.CreateNotPlacedRooms");

            PushButton pb22 = ribbonPanelb2.AddItem(b22Data) as PushButton;
            pb22.ToolTip = "Создать помещения в проекте по Excel файлу на диске С";

            BitmapImage pb22Image = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/icons8-rooms-32.png"));
            pb22.LargeImage = pb22Image;

            //попробуем создать еще одну кнопку
            ribbonPanelb2.AddSeparator(); //разделитель между кнопками

            // create push button for CurveTotalLength  //namespace и класс
            PushButtonData b24Data = new PushButtonData("cmdDelUnplacedRoom.cs", "Удалить"
                + Environment.NewLine + "лишнии помещения", thisAssemblyPath, "BIMv2.DelUnplacedRoom");

            PushButton pb24 = ribbonPanelb2.AddItem(b24Data) as PushButton;
            pb24.ToolTip = "Удаляет лишнии помещения из проекта, чтобы было красиво";

            BitmapImage pb24Image = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/icons8-clean2-32.png"));
            pb24.LargeImage = pb24Image;

            //попробуем создать еще одну кнопку
            ribbonPanelb2.AddSeparator(); //разделитель между кнопками

            // create push button for CurveTotalLength  //namespace и класс
            PushButtonData b23Data = new PushButtonData("cmdWriteTypeRooms.cs", "Записать"
                + Environment.NewLine + "тип квартиры", thisAssemblyPath, "BIMv2.WriteTypeRooms");

            PushButton pb23 = ribbonPanelb2.AddItem(b23Data) as PushButton;
            pb23.ToolTip = "Записать тип квартиры";

            BitmapImage pb23Image = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/icons8-typeRoom-32.png"));
            pb23.LargeImage = pb23Image;

            //попробуем создать еще одну кнопку
            ribbonPanelb2.AddSeparator(); //разделитель между кнопками

            // create push button for CurveTotalLength  //namespace и класс
            PushButtonData b1DataB2_2 = new PushButtonData("cmdCreteFinishWalls.cs", "Создать отделку"
                + Environment.NewLine + "в помещении", thisAssemblyPath, "BIMv2.CreteFinishWalls");

            PushButton pb1B2_2 = ribbonPanelb2.AddItem(b1DataB2_2) as PushButton;
            pb1B2_2.ToolTip = "Создать отделочные стены в помещении";

            BitmapImage pb1ImageB2_2 = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/icons8-finishwall-32.png"));
            pb1B2_2.LargeImage = pb1ImageB2_2;

            //попробуем создать еще одну кнопку
            ribbonPanelb2.AddSeparator(); //разделитель между кнопками

            // create push button for CurveTotalLength  //namespace и класс
            PushButtonData b1DataB2_1 = new PushButtonData("cmdConvertGrids3Dto2D", "Оси 2D/3D"
                + Environment.NewLine + "поменять", thisAssemblyPath, "BIMv2.ConvertGrids3Dto2D");

            PushButton pb1B2_1 = ribbonPanelb2.AddItem(b1DataB2_1) as PushButton;
            pb1B2_1.ToolTip = "Перевести оси в 2D либо 3D";

            BitmapImage pb1ImageB2_1 = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/icons8-2d-16.png"));
            pb1B2_1.LargeImage = pb1ImageB2_1;

            //попробуем создать еще одну кнопку
            ribbonPanelb2.AddSeparator(); //разделитель между кнопками

            // create push button for CurveTotalLength  //namespace и класс
            PushButtonData b1DataB3_1 = new PushButtonData("cmdCreateNewTypeAnnotation", "Создать"
                + Environment.NewLine + "флажок", thisAssemblyPath, "BIMv2.CreateNewTypeAnnotation");

            PushButton pb1B3_1 = ribbonPanelb2.AddItem(b1DataB3_1) as PushButton;
            pb1B3_1.ToolTip = "Создает и обновляет флажки";

            BitmapImage pb1ImageB3_1 = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/icons8-flag-32.png"));
            pb1B3_1.LargeImage = pb1ImageB3_1;

            //попробуем создать еще одну кнопку
            ribbonPanelb2.AddSeparator(); //разделитель между кнопками

            // create push button for CurveTotalLength  //namespace и класс
            PushButtonData b1DataB3_2 = new PushButtonData("cmdCDoorsInformationTransfer", "Заполнить"
                + Environment.NewLine + "двери", thisAssemblyPath, "BIMv2.DoorsInformationTransfer");

            PushButton pb1B3_2 = ribbonPanelb2.AddItem(b1DataB3_2) as PushButton;
            pb1B3_2.ToolTip = "Заполняем дверь";

            BitmapImage pb1ImageB3_2 = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/icons8-door-32.png"));
            pb1B3_2.LargeImage = pb1ImageB3_2;

            //попробуем создать еще одну кнопку
            ribbonPanelb2.AddSeparator(); //разделитель между кнопками

            // create push button for CurveTotalLength  //namespace и класс
            PushButtonData b1DataB3_3 = new PushButtonData("cmdMakingHolesForWindows", "Отверстия"
                + Environment.NewLine + "для окон", thisAssemblyPath, "BIMv2.MakingHolesForWindows");

            PushButton pb1B3_3 = ribbonPanelb2.AddItem(b1DataB3_3) as PushButton;
            pb1B3_3.ToolTip = "Создаем отверстия и обновляем тут";

            BitmapImage pb1ImageB3_3 = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/icons8-win-32.png"));
            pb1B3_3.LargeImage = pb1ImageB3_3;

            //попробуем создать еще одну кнопку
            ribbonPanelb2.AddSeparator(); //разделитель между кнопками

            // create push button for CurveTotalLength  //namespace и класс
            PushButtonData b1DataB3_4 = new PushButtonData("cmdMakingHolesForFacadeWindows", "Отверстия"
                + Environment.NewLine + "для окон в фасаде", thisAssemblyPath, "BIMv2.MakingHolesForFacadeWindows");

            PushButton pb1B3_4 = ribbonPanelb2.AddItem(b1DataB3_4) as PushButton;
            pb1B3_4.ToolTip = "Создаем отверстия в фасадном файле";

            BitmapImage pb1ImageB3_4 = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/icons8-win-32.png"));
            pb1B3_4.LargeImage = pb1ImageB3_4;

            #endregion

            //кнопки 2BIM

            #region BIMv2


            String tabName = "BIMv2";    // создание вкладки
            application.CreateRibbonTab(tabName);

            // Add a new ribbon panel
            RibbonPanel ribbonPanel = application.CreateRibbonPanel(tabName, "Надстройки для архитектора"); //подпись под кнопками

           

            // create push button for CurveTotalLength  //namespace и класс
            PushButtonData b1Data = new PushButtonData("cmdWriteNameRoomInPlumbingFixtures", "Помещения" 
                + Environment.NewLine + "в элементы", thisAssemblyPath, "BIMv2.WriteNameRoomInPlumbingFixtures");

            PushButton pb1 = ribbonPanel.AddItem(b1Data) as PushButton;
            pb1.ToolTip = "Переписывает номера и имена в ADSK_Зона для сантехники, обобщенных моделей, спец.оборудование, мебель";

            BitmapImage pb1Image = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/transferNameRoom.png"));
            pb1.LargeImage = pb1Image;
            

            //попробуем создать еще одну кнопку
            ribbonPanel.AddSeparator(); //разделитель между кнопками

            
            

            // create push button for CurveTotalLength  //namespace и класс
            PushButtonData b21Data = new PushButtonData("cmdWallFinishing", "Посчитать"
                + Environment.NewLine + "отделку", thisAssemblyPath, "BIMv2Buttons.WallFinishing2");

            PushButton pb21 = ribbonPanel.AddItem(b21Data) as PushButton;
            pb21.ToolTip = "Переписывает отделку в помещения";

            BitmapImage pb21Image = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/icons8-floor-32.png"));
            pb21.LargeImage = pb21Image;


            //попробуем создать еще одну кнопку
            ribbonPanel.AddSeparator(); //разделитель между кнопками

            // Create three push buttons for split button drop down 



            // #1 
            PushButtonData pushButtonData1 = new PushButtonData("cmdGroupTypeFloorByNote.cs", "Сгруппировать полы", thisAssemblyPath, "BIMv2Buttons.GroupTypeFloorByNote");
            pushButtonData1.LargeImage = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/icons8-floor-32.png"));

            // #2 
            PushButtonData pushButtonData2 = new PushButtonData("cmdGroupTypeCeilingByNote.cs", "Сгруппировать потолки", thisAssemblyPath, "BIMv2Buttons.GroupTypeCeilingByNote");
            pushButtonData2.LargeImage = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/icons8-ceiling-32.png"));

            // #3 
            PushButtonData pushButtonData3 = new PushButtonData("cmdGroupTypeWallsByNote.cs", "Сгруппировать стены", thisAssemblyPath, "BIMv2Buttons.GroupTypeWallsByNote");
            pushButtonData3.LargeImage = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/icons8-wall-32.png"));

            // #4 
            PushButtonData pushButtonData4 = new PushButtonData("cmdGroupByMarkEqupmentFurnitureAndWriteToComment.cs", "Сгруппировать оборудование и мебель", thisAssemblyPath, "BIMv2.GroupByMarkEqupmentFurnitureAndWriteToComment");
            pushButtonData4.LargeImage = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/icons8-groupCoins-32.png"));

            // Make a split button now 
            SplitButtonData splitBtnData = new SplitButtonData("SplitButton", "Split Button");
            SplitButton splitBtn = ribbonPanel.AddItem(splitBtnData) as SplitButton;

            splitBtn.AddPushButton(pushButtonData1);
            splitBtn.AddPushButton(pushButtonData2);
            splitBtn.AddPushButton(pushButtonData3);
            splitBtn.AddPushButton(pushButtonData4);

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
            ribbonPanel.AddSeparator(); //разделитель между кнопками

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


            // Make a split button now 
            SplitButtonData splitBtnData2 = new SplitButtonData("SplitButton2", "Split Button2");
            SplitButton splitBtn2 = ribbonPanel.AddItem(splitBtnData2) as SplitButton;

            splitBtn2.AddPushButton(b12Data);
            splitBtn2.AddPushButton(b13Data);
            splitBtn2.AddPushButton(b14Data);
            splitBtn2.AddPushButton(b15Data);

            //попробуем создать еще одну кнопку
            ribbonPanel.AddSeparator(); //разделитель между кнопками
            

            // create push button for CurveTotalLength  //namespace и класс
            PushButtonData b3Data = new PushButtonData("cmdCreateModelText.cs", "Создать"
                + Environment.NewLine + "текст модели", thisAssemblyPath, "BIMv2.CreateModelText");

            PushButton pb3 = ribbonPanel.AddItem(b3Data) as PushButton;
            pb3.ToolTip = "Создать текст модели на основе .ТХТ файла с диска С";

            BitmapImage pb3Image = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/creteModelText.png"));
            pb3.LargeImage = pb3Image;

            
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
            //RibbonPanel ribbonPanel3 = application.CreateRibbonPanel(tabName, "Надстройки для МЕГА-электрика"); //подпись под кнопками

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


            // Add a new ribbon panel
            RibbonPanel ribbonPanel4 = application.CreateRibbonPanel(tabName, "Надстройки для всех"); //подпись под кнопками

            // create push button for CurveTotalLength  //namespace и класс
            //PushButtonData b7Data = new PushButtonData("cmdCreateAnyElementsInRooms", "Расставить"
            //    + Environment.NewLine + "элементы в помещениях", thisAssemblyPath, "BIMv2.CreateAnyElementsInRooms");

            PushButtonData b7Data = new PushButtonData("cmdCreateElementsInRooms", "Расставить"
                + Environment.NewLine + "элементы в помещениях", thisAssemblyPath, "BIMv2.CreateElementsInRooms");

            PushButton pb7 = ribbonPanel4.AddItem(b7Data) as PushButton;
            pb7.ToolTip = "Расставить выбранное семейство в помещениях";

            BitmapImage pb7Image = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/icons8-crane-machine-32.png"));
            pb7.LargeImage = pb7Image;



            //попробуем создать еще одну кнопку
            ribbonPanel4.AddSeparator(); //разделитель между кнопками

            PushButtonData b9Data = new PushButtonData("cmdDeletedFilter", "Удалить"
                + Environment.NewLine + "фильтры из проекта", thisAssemblyPath, "BIMv2.DeletedFilter");

            PushButton pb9 = ribbonPanel4.AddItem(b9Data) as PushButton;
            pb9.ToolTip = "Удалить фильтры";

            BitmapImage pb9Image = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/icons8-clean-32.png"));
            pb9.LargeImage = pb9Image;


            //попробуем создать еще одну кнопку
            ribbonPanel4.AddSeparator(); //разделитель между кнопками


            PushButtonData b10Data = new PushButtonData("cmdDeletedUnFilter", "Удалить"
                                        + Environment.NewLine + "неиспользуемые фильтры", thisAssemblyPath, "BIMv2.DeletedUnFilter");

            PushButton pb10 = ribbonPanel4.AddItem(b10Data) as PushButton;
            pb10.ToolTip = "Удалить только неиспользуемые фильтры";

            BitmapImage pb10Image = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/icons8-clean2-32.png"));
            pb10.LargeImage = pb10Image;

            //попробуем создать еще одну кнопку
            ribbonPanel4.AddSeparator(); //разделитель между кнопками

            PushButtonData b11Data = new PushButtonData("cmdClearProjectBrowser", "Удалить"
                                        + Environment.NewLine + "всё из диспетчера", thisAssemblyPath, "BIMv2.ClearProjectBrowser");

            PushButton pb11 = ribbonPanel4.AddItem(b11Data) as PushButton;
            pb11.ToolTip = "Удалить всё кроме активного вида";

            BitmapImage pb11Image = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/icons8-clean3-32.png"));
            pb11.LargeImage = pb11Image;

            //попробуем создать еще одну кнопку
            ribbonPanel4.AddSeparator(); //разделитель между кнопками

            PushButtonData b17Data = new PushButtonData("cmdCreateWorksets", "Создать"
                + Environment.NewLine + "рабочие наборы", thisAssemblyPath, "BIMv2.CreateWorksets");

            PushButton pb17 = ribbonPanel4.AddItem(b17Data) as PushButton;
            pb17.ToolTip = "Создание рабочих наборов по списку";

            BitmapImage pb17Image = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/icons8-ws-32.png"));
            pb17.LargeImage = pb17Image;

            //попробуем создать еще одну кнопку
            ribbonPanel4.AddSeparator(); //разделитель между кнопками

            PushButtonData b20Data = new PushButtonData("cmdCreateModelTextRoomNameInRoom.cs", "Подписать"
                + Environment.NewLine + "помещения в 3D", thisAssemblyPath, "BIMv2.CreateModelTextRoomNameInRoom");

            PushButton pb20 = ribbonPanel4.AddItem(b20Data) as PushButton;
            pb20.ToolTip = "Подписать помещения с помощью семейства Для подписи помещений.rfa";

            BitmapImage pb20Image = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/icons8-sign-32.png"));
            pb20.LargeImage = pb20Image;

            //попробуем создать еще одну кнопку
            ribbonPanel4.AddSeparator(); //разделитель между кнопками


            PushButtonData b25Data = new PushButtonData("cmdDuplicateSheets.cs", "Скопировать"
                + Environment.NewLine + "лист с видами", thisAssemblyPath, "BIMv2.DuplicateSheets");

            PushButton pb25 = ribbonPanel4.AddItem(b25Data) as PushButton;
            pb25.ToolTip = "Скопировать лист с видами размещенным на нем";

            BitmapImage pb25Image = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/icons8-sheets-32.png"));
            pb25.LargeImage = pb25Image;

            //попробуем создать еще одну кнопку
            ribbonPanel4.AddSeparator(); //разделитель между кнопками

            PushButtonData b26Data = new PushButtonData("cmdCreateViewByLinkedFiles.cs", "Создать 3D"
               + Environment.NewLine + "связей", thisAssemblyPath, "BIMv2.CreateViewByLinkedFiles");

            PushButton pb26 = ribbonPanel4.AddItem(b26Data) as PushButton;
            pb26.ToolTip = "Создать 3D виды для связанных файлов";

            BitmapImage pb26Image = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/icons8-3d-modeling-32.png"));
            pb26.LargeImage = pb26Image;

            //попробуем создать еще одну кнопку
            ribbonPanel4.AddSeparator(); //разделитель между кнопками


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
    }
}

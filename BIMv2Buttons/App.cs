using System;
using System.Collections.Generic;
using System.Reflection;
using Autodesk.Revit.UI;
using System.Windows.Media.Imaging;

namespace BIMv2Buttons
{
    public class App : IExternalApplication
    {
        // define a method that will create our tab and button
        static void AddRibbonPanel(UIControlledApplication application)
        {

            String tabName = "BIMv2";    // создание вкладки
            application.CreateRibbonTab(tabName);

            // Add a new ribbon panel
            RibbonPanel ribbonPanel = application.CreateRibbonPanel(tabName, "Надстройки для архитектора"); //подпись под кнопками

            // Get dll assembly path
            string thisAssemblyPath = Assembly.GetExecutingAssembly().Location;

            // create push button for CurveTotalLength  //namespace и класс
            PushButtonData b1Data = new PushButtonData("cmdWriteNameRoomInPlumbingFixtures", "Помещения" 
                + Environment.NewLine + "в элементы", thisAssemblyPath, "BIMv2.WriteNameRoomInPlumbingFixtures");

            PushButton pb1 = ribbonPanel.AddItem(b1Data) as PushButton;
            pb1.ToolTip = "Переписывает номера и имена в ADSK_Зона для сантехники, обобщенных моделей, спец.оборудование, мебель";

            BitmapImage pb1Image = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/transferNameRoom.png"));
            pb1.LargeImage = pb1Image;
            

            //попробуем создать еще одну кнопку
            ribbonPanel.AddSeparator(); //разделитель между кнопками

            // Create three push buttons for split button drop down 

            
            

            // create push button for CurveTotalLength  //namespace и класс
            PushButtonData b3Data = new PushButtonData("cmdCreateModelText.cs", "Создать"
                + Environment.NewLine + "текст модели", thisAssemblyPath, "BIMv2.CreateModelText");

            PushButton pb3 = ribbonPanel.AddItem(b3Data) as PushButton;
            pb3.ToolTip = "Создать текст модели на основе .ТХТ файла с диска С";

            BitmapImage pb3Image = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/creteModelText.png"));
            pb3.LargeImage = pb3Image;

            
           
            

            // Add a new ribbon panel
            RibbonPanel ribbonPanel3 = application.CreateRibbonPanel(tabName, "Надстройки для МЕГА-электрика"); //подпись под кнопками

            // create push button for CurveTotalLength  //namespace и класс
            PushButtonData b2Data = new PushButtonData("cmdSumAllLightingFixtureInRoom", "Посчитать"
                + Environment.NewLine + "светильники", thisAssemblyPath, "BIMv2.SumAllLightingFixtureInRoom");

            PushButton pb2 = ribbonPanel3.AddItem(b2Data) as PushButton;
            pb2.ToolTip = "Подсчет количества светильников в помещении и запись в марку ADSK_Количество светильников";

            BitmapImage pb2Image = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/countLampInRoom.png"));
            pb2.LargeImage = pb2Image;

            //попробуем создать еще одну кнопку
            ribbonPanel3.AddSeparator(); //разделитель между кнопками

            //16

            // create push button for CurveTotalLength  //namespace и класс
            PushButtonData b16Data = new PushButtonData("cmdNormalIlluminationForLightFix.cs", "Записать норм."
                + Environment.NewLine + "освещенность", thisAssemblyPath, "BIMv2.NormalIlluminationForLightFix");

            PushButton pb16 = ribbonPanel3.AddItem(b16Data) as PushButton;
            pb16.ToolTip = "Запись нормируемой освещенности";

            BitmapImage pb16Image = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/countLampInRoom2.png"));
            pb16.LargeImage = pb16Image;

            //попробуем создать еще одну кнопку
            ribbonPanel3.AddSeparator(); //разделитель между кнопками

            // create push button for CurveTotalLength  //namespace и класс
            PushButtonData b6Data = new PushButtonData("cmdCreateLightingInRoom", "Расставить"
                + Environment.NewLine + "светильники", thisAssemblyPath, "BIMv2.CreateLightingInRoom");

            PushButton pb6 = ribbonPanel3.AddItem(b6Data) as PushButton;
            pb6.ToolTip = "Размещает светильники в помещениях";

            BitmapImage pb6Image = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/icons8-oil-lamp-32.png"));
            pb6.LargeImage = pb6Image;

            //попробуем создать еще одну кнопку
            ribbonPanel3.AddSeparator(); //разделитель между кнопками

            // create push button for CurveTotalLength  //namespace и класс
            PushButtonData b8Data = new PushButtonData("cmdPowerFactorElectrical.cs", "Посчитать"
                + Environment.NewLine + "cos фи (φ)", thisAssemblyPath, "BIMv2.PowerFactorElectrical");

            PushButton pb8 = ribbonPanel3.AddItem(b8Data) as PushButton;
            pb8.ToolTip = "Расчет cos фи (φ)";

            BitmapImage pb8Image = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/icons8-cos-32.png"));
            pb8.LargeImage = pb8Image;


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

            // Create three push buttons for split button drop down 

            // #1 
            PushButtonData ribbonItemData1 = new PushButtonData("ConvertGrids3Dto2D", "Оси 2D/3D", thisAssemblyPath, "BIMv2Buttons.ConvertGrids3Dto2D");
            ribbonItemData1.Image = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/icons8-2d-16.png"));

            // #2 
            PushButtonData ribbonItemData2 = new PushButtonData("HelpBIM.cs", "2BIM.RU", thisAssemblyPath, "BIMv2.HelpBIM");
            ribbonItemData2.Image = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/icons8-whatsapp-32.png"));

            
            // Create a ribbon panel
            RibbonPanel m_projectPanel = application.CreateRibbonPanel(tabName, "Разное");

            // Add the buttons to the panel
            List<RibbonItem> projectButtons = new List<RibbonItem>();
            projectButtons.AddRange(m_projectPanel.AddStackedItems(ribbonItemData1, ribbonItemData2));
            
            m_projectPanel.AddSeparator();

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

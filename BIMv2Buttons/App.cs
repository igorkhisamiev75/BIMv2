using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;

using Autodesk.Revit.DB;
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
            

            // create push button for CurveTotalLength  //namespace и класс
            PushButtonData b3Data = new PushButtonData("cmdCreateModelText.cs", "Создать"
                + Environment.NewLine + "текст модели", thisAssemblyPath, "BIMv2.CreateModelText");

            PushButton pb3 = ribbonPanel.AddItem(b3Data) as PushButton;
            pb3.ToolTip = "Создать текст модели на основе .ТХТ файла с диска С";

            BitmapImage pb3Image = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/creteModelText.png"));
            pb3.LargeImage = pb3Image;

            
           
            // Add a new ribbon panel
            RibbonPanel ribbonPanel2 = application.CreateRibbonPanel(tabName, "BIM стандарт и помощь"); //подпись под кнопками

            // create push button for CurveTotalLength  //namespace и класс
            PushButtonData b4Data = new PushButtonData("cmdBIMStandart.cs", "Почитать"
                + Environment.NewLine + "BIM стандарт", thisAssemblyPath, "BIMv2.BIMStandart");

            PushButton pb4 = ribbonPanel2.AddItem(b4Data) as PushButton;
            pb4.ToolTip = "Открой хотя бы раз";

            BitmapImage pb4Image = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/icons8-book-32.png"));
            pb4.LargeImage = pb4Image;

            ribbonPanel2.AddSeparator(); //разделитель между кнопками

            // create push button for CurveTotalLength  //namespace и класс
            PushButtonData b5Data = new PushButtonData("cmdHelpBIM.cs", "Написать"
                + Environment.NewLine + "в тех поддержку", thisAssemblyPath, "BIMv2.HelpBIM");

            PushButton pb5 = ribbonPanel2.AddItem(b5Data) as PushButton;
            pb5.ToolTip = "Можно написать, но лучше поискать самим";

            BitmapImage pb5Image = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/icons8-whatsapp-32.png"));
            pb5.LargeImage = pb5Image;

            

            // Add a new ribbon panel
            RibbonPanel ribbonPanel3 = application.CreateRibbonPanel(tabName, "Надстройки для электрика"); //подпись под кнопками

            // create push button for CurveTotalLength  //namespace и класс
            PushButtonData b2Data = new PushButtonData("cmdSumAllLightingFixtureInRoom", "Посчитать"
                + Environment.NewLine + "светильники", thisAssemblyPath, "BIMv2.SumAllLightingFixtureInRoom");

            PushButton pb2 = ribbonPanel3.AddItem(b2Data) as PushButton;
            pb2.ToolTip = "Подсчет количества светильников в помещении и запись в марку ADSK_Количество светильников";

            BitmapImage pb2Image = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/countLampInRoom.png"));
            pb2.LargeImage = pb2Image;

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



            //

            PushButtonData b9Data = new PushButtonData("cmdDeletedFilter", "Удалить"
                + Environment.NewLine + "фильтры из проекта", thisAssemblyPath, "BIMv2.DeletedFilter");

            PushButton pb9 = ribbonPanel4.AddItem(b9Data) as PushButton;
            pb9.ToolTip = "Удалить фильтры";

            BitmapImage pb9Image = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/icons8-clean-32.png"));
            pb9.LargeImage = pb9Image;


            //попробуем создать еще одну кнопку
            ribbonPanel4.AddSeparator(); //разделитель между кнопками



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

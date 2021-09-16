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
            RibbonPanel ribbonPanel = application.CreateRibbonPanel(tabName, "Надстройки MRGT v1.0"); //подпись под кнопками

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
            PushButtonData b2Data = new PushButtonData("cmdSumAllLightingFixtureInRoom", "Посчитать" 
                + Environment.NewLine + "светильники", thisAssemblyPath, "BIMv2.SumAllLightingFixtureInRoom");

            PushButton pb2 = ribbonPanel.AddItem(b2Data) as PushButton;
            pb2.ToolTip = "Подсчет количества светильников в помещении и запись в марку ADSK_Количество светильников";

            BitmapImage pb2Image = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/countLampInRoom.png"));
            pb2.LargeImage = pb2Image;

            //попробуем создать еще одну кнопку
            ribbonPanel.AddSeparator(); //разделитель между кнопками
            

            // create push button for CurveTotalLength  //namespace и класс
            PushButtonData b3Data = new PushButtonData("cmdCreateModelText.cs", "Создать"
                + Environment.NewLine + "текст модели", thisAssemblyPath, "BIMv2.CreateModelText");

            PushButton pb3 = ribbonPanel.AddItem(b3Data) as PushButton;
            pb3.ToolTip = "Создать текст модели на основе .ТХТ файла с диска С";

            BitmapImage pb3Image = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/creteModelText.png"));
            pb3.LargeImage = pb3Image;

            //попробуем создать еще одну кнопку
            ribbonPanel.AddSeparator(); //разделитель между кнопками
           
            // Add a new ribbon panel
            RibbonPanel ribbonPanel2 = application.CreateRibbonPanel(tabName, "BIM Standart and HELP"); //подпись под кнопками

            // create push button for CurveTotalLength  //namespace и класс
            PushButtonData b4Data = new PushButtonData("cmdBIMStandart.cs", "Почитать"
                + Environment.NewLine + "BIM стандарт", thisAssemblyPath, "BIMv2.BIMStandart");

            PushButton pb4 = ribbonPanel2.AddItem(b4Data) as PushButton;
            pb4.ToolTip = "Открой хотя бы раз";

            BitmapImage pb4Image = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/icons8-book-32.png"));
            pb4.LargeImage = pb4Image;

            ribbonPanel.AddSeparator(); //разделитель между кнопками

            // create push button for CurveTotalLength  //namespace и класс
            PushButtonData b5Data = new PushButtonData("cmdHelpBIM.cs", "Написать"
                + Environment.NewLine + "в тех поддержку", thisAssemblyPath, "BIMv2.HelpBIM");

            PushButton pb5 = ribbonPanel2.AddItem(b5Data) as PushButton;
            pb5.ToolTip = "Можно написать, но лучше поискать самим";

            BitmapImage pb5Image = new BitmapImage(new Uri("pack://application:,,,/BIMv2Buttons;component/Resources/icons8-whatsapp-32.png"));
            pb5.LargeImage = pb5Image;

            ribbonPanel.AddSeparator(); //разделитель между кнопками

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

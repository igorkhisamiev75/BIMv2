using Autodesk.Revit.UI;

namespace LintelPlacer
{
    public class LintelPlacerApplication : IExternalApplication
    {
        public Result OnStartup(UIControlledApplication application)
        {
            var panel = application.CreateRibbonPanel("Перемычки");

            var buttonData = new PushButtonData(
                "LintelPlacer",
                "Расставить перемычки",
                typeof(LintelPlacerCommand).Assembly.Location,
                typeof(LintelPlacerCommand).FullName);

            panel.AddItem(buttonData);

            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            return Result.Succeeded;
        }
    }
}
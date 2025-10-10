using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.DocumentParts;
using Autodesk.Navisworks.Api.Plugins;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;


namespace BIMv2Navis


{
    public class MainClass : AddInPlugin
    {
        public override int Execute(params string[] parameters)
        {
            try
            {
                Document document = Autodesk.Navisworks.Api.Application.ActiveDocument;

                if (document == null)
                {
                    MessageBox.Show("No active document found.", "Information",
                                  MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return 0;
                }

                StringBuilder messageBuilder = new StringBuilder();
                messageBuilder.AppendLine($"Current File: {document.CurrentFileName}");

                DocumentModels models = document.Models;

                if (models.Count == 0)
                {
                    messageBuilder.AppendLine("No appended files.");
                }
                else
                {
                    messageBuilder.AppendLine("Appended Files:");

                    for (int i = 0; i < models.Count; i++)
                    {
                        Model model = models[i];
                        messageBuilder.AppendLine($"         {i + 1}. {model.FileName}");
                    }
                }

                MessageBox.Show(messageBuilder.ToString(), "Document Information",
                              MessageBoxButtons.OK, MessageBoxIcon.Information);

                return 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Error",
                              MessageBoxButtons.OK, MessageBoxIcon.Error);
                return -1;
            }
        }
    }
}

using System;

using Avalonia.Controls;
using Avalonia.Controls.Templates;

using Resona.UI.ViewModels;

using Serilog;

namespace Resona.UI
{
    public class DataTemplateViewLocator : IDataTemplate
    {
        public Control Build(object? data)
        {
            var typeName = data?.GetType().FullName;
            if (typeName == null)
            {
                Log.Error("Can't build a view for a null view model");

                throw new ArgumentNullException(nameof(data), "data cannot be null");
            }

            var name = typeName.Replace("ViewModel", "View");

            Log.Debug("Building view {ViewTypeName} for {ViewModelTypeName}", typeName, name);

            var type = Type.GetType(name);

            return type != null ? (Control)Activator.CreateInstance(type)! : new TextBlock { Text = "Not Found: " + name };
        }

        public bool Match(object? data)
        {
            return data is RoutableViewModelBase;
        }
    }
}

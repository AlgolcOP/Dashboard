using System;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Dashboard.ViewModels;

namespace Dashboard
{
    /// <summary>
    /// 视图定位器，用于根据ViewModel定位到对应的View
    /// </summary>
    public class ViewLocator : IDataTemplate
    {
        public Control? Build(object? param)
        {
            if (param is null)
            {
                return null;
            }

            var name = param.GetType().FullName!.Replace("ViewModel", "View", StringComparison.Ordinal);
            var type = Type.GetType(name);

            if (type != null)
            {
                return (Control)Activator.CreateInstance(type)!;
            }

            return new TextBlock { Text = "Not Found: " + name };
        }

        public bool Match(object? data) => data is ViewModelBase;
    }
}
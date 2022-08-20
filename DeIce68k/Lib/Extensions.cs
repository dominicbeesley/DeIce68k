using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace DeIce68k.Lib
{
    public static class Extensions
    {
        public static T GetDescendantByType<T>(this Visual element, int maxLevels = -1) where T : class
        {
            if (element == null)
            {
                return default(T);
            }
            if (element.GetType() == typeof(T))
            {
                return element as T;
            }
            T foundElement = null;
            if (element is FrameworkElement)
            {
                (element as FrameworkElement).ApplyTemplate();
            }
            if (maxLevels > 0)
                maxLevels--;
            if (maxLevels == 0)
                return null;
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
            {
                var visual = VisualTreeHelper.GetChild(element, i) as Visual;
                foundElement = visual.GetDescendantByType<T>(maxLevels);
                if (foundElement != null)
                {
                    break;
                }
            }
            return foundElement;
        }

    }
}

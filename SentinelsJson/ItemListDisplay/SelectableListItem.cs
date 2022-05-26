using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using SolidShineUi;

namespace SentinelsJson.Ild
{
    public abstract class SelectableListItem : SelectableUserControl
    {

        public event EventHandler? RequestMoveUp;
        public event EventHandler? RequestMoveDown;
        public event EventHandler? RequestDelete;
        public event EventHandler? ContentChanged; // event just to update main window's "isDirty" value

        public abstract void LoadValues(Dictionary<IldPropertyInfo, object> properties);

        public abstract object? GetPropertyValue(IldPropertyInfo property);

        public abstract Dictionary<string, object> GetAllProperties();

        public void DoRequestDelete()
        {
            RequestDelete?.Invoke(this, EventArgs.Empty);
        }

        public void DoRequestMoveUp()
        {
            RequestMoveUp?.Invoke(this, EventArgs.Empty);
        }

        public void DoRequestMoveDown()
        {
            RequestMoveDown?.Invoke(this, EventArgs.Empty);
        }

        public void DoContentChanged()
        {
            ContentChanged?.Invoke(this, EventArgs.Empty);
        }

        public void RequestDeleteEventHandler(object sender, RoutedEventArgs e)
        {
            DoRequestDelete();
        }
    }
}

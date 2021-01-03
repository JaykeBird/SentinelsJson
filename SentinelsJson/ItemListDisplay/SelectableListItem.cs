using System;
using System.Collections.Generic;
using System.Text;
using SolidShineUi;

namespace SentinelsJson.Ild
{
    public abstract class SelectableListItem : SelectableUserControl
    {

        public event EventHandler? RequestMoveUp;
        public event EventHandler? RequestMoveDown;
        public event EventHandler? RequestDelete;

        public abstract void MapProperties(Dictionary<IldPropertyInfo, object> properties);


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
    }
}

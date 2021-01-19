using SentinelsJson.Ild;
using SolidShineUi;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Reflection;

namespace SentinelsJson.Ild
{
    /// <summary>
    /// Interaction logic for ItemListDisplay.xaml
    /// </summary>
    public partial class ItemListDisplay : UserControl
    {
        public ItemListDisplay()
        {
            InitializeComponent();
        }

        public event EventHandler? ContentChanged;

        #region ColorScheme

        public event DependencyPropertyChangedEventHandler? ColorSchemeChanged;

        public static DependencyProperty ColorSchemeProperty
            = DependencyProperty.Register("ColorScheme", typeof(ColorScheme), typeof(ItemListDisplay),
            new FrameworkPropertyMetadata(new ColorScheme(), new PropertyChangedCallback(OnColorSchemeChanged)));

        public static void OnColorSchemeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ColorScheme cs = (e.NewValue as ColorScheme)!;

            if (d is ItemListDisplay s)
            {
                s.ColorSchemeChanged?.Invoke(d, e);
                s.ApplyColorScheme(cs);
            }
        }

        public ColorScheme ColorScheme
        {
            get => (ColorScheme)GetValue(ColorSchemeProperty);
            set => SetValue(ColorSchemeProperty, value);
        }

        public void ApplyColorScheme(ColorScheme cs)
        {
            if (cs != ColorScheme)
            {
                ColorScheme = cs;
                return;
            }

            BorderBrush = cs.BorderColor.ToBrush();
        }

        #endregion

        public string Title
        {
            get => txtTitle.Text;
            set => txtTitle.Text = value;
        }

        public Type? SheetClassType
        {
            get => sheetType;
            set
            {
                sheetType = value;
                if (value != null)
                {
                    propertyNames = ListProperties(value);
                    LoadFilterMenu();
                }
                else
                {
                    propertyNames.Clear();
                    btnFilter.Menu = null;
                }
            }
        }

        private Type? sheetType;
        private Type? displayElement;

        private static Type SELECTABLE_ITEM_TYPE = typeof(SelectableListItem);

        private List<IldPropertyInfo> propertyNames = new List<IldPropertyInfo>();

        public Type? DisplayElementType
        {
            get
            {
                return displayElement;
            }
            set
            {
                if (SELECTABLE_ITEM_TYPE.IsAssignableFrom(value))
                {
                    displayElement = value;
                }
                else
                {
                    throw new ArgumentException("Entered type must derive from the SelectableListItem type.");
                }
            }
        }

        public void LoadList<T>(IEnumerable<T> items)
        {
            if (typeof(T) != SheetClassType)
            {
                throw new ArgumentException("Designated generic type does not match SheetClassType property.");
            }

            if (DisplayElementType == null)
            {
                throw new InvalidOperationException("DisplayElementType is not set");
            }

            foreach (T item in items)
            {
                var newItem = Activator.CreateInstance(DisplayElementType);

                SelectableListItem sli = (SelectableListItem)newItem!;

                sli.MapProperties(GenerateMappedProperties(item));

                sli.RequestDelete += sli_RequestDelete;
                sli.RequestMoveDown += sli_RequestMoveDown;
                sli.RequestMoveUp += sli_RequestMoveUp;

                selPanel.AddItem(sli);
            }
        }

        //private List<string> ListProperties<T>(T item)
        //{
        //    Type type = typeof(T);
        //    return ListProperties(type);
        //}

        private List<IldPropertyInfo> ListProperties(Type type)
        {
            List<IldPropertyInfo> props = new List<IldPropertyInfo>();

            foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {

                string name = property.Name;
                var attr = property.GetCustomAttribute<IldDisplayAttribute>();

                int? minValue = null;
                int? maxValue = null;

                if (attr != null)
                {
                    if (attr.Ignore) continue;
                    if (attr.Name != null) name = attr.Name;
                    minValue = attr.MinValue;
                    maxValue = attr.MaxValue;
                }

                Type pt = property.PropertyType;

                IldType ildType;

                if (pt == typeof(string))
                {
                    ildType = IldType.String;
                }
                else if (pt == typeof(bool))
                {
                    ildType = IldType.Boolean;
                }
                else if (pt == typeof(int))
                {
                    ildType = IldType.Integer;
                }
                else if (pt == typeof(double))
                {
                    ildType = IldType.Double;
                }
                else
                {
                    throw new NotSupportedException("This property uses a type that isn't supported by the ItemListDisplay.");
                }

                IldPropertyInfo prop = new IldPropertyInfo(property.Name, ildType, name);
                prop.MinValue = minValue;
                prop.MaxValue = maxValue;
                props.Add(prop);
            }

            return props;
        }

        private Dictionary<IldPropertyInfo, object> GenerateMappedProperties<T>(T item)
        {
            Dictionary<IldPropertyInfo, object> props = new Dictionary<IldPropertyInfo, object>();
            Type type = typeof(T);

            foreach (IldPropertyInfo prop in propertyNames)
            {
                PropertyInfo? property = type.GetProperty(prop.Name);

                if (property == null) continue;

                //var attr = property.GetCustomAttribute<IldDisplayAttribute>();

                //if (attr != null)
                //{
                //    if (attr.Ignore) continue;
                //}

                object? val = property.GetValue(item);

                if (val == null)
                {
                    // set default values if val is null
                    switch (prop.IldType)
                    {
                        case IldType.String:
                            val = "";
                            break;
                        case IldType.Integer:
                            val = 0;
                            break;
                        case IldType.Double:
                            val = 0d;
                            break;
                        case IldType.Boolean:
                            val = false;
                            break;
                        default:
                            val = "";
                            break;
                    }
                }

                props.Add(prop, val);
            }

            return props;
        }

        private void LoadFilterMenu()
        {
            var cm = new SolidShineUi.ContextMenu();

            foreach (var item in propertyNames)
            {
                MenuItem mi = new MenuItem();
                mi.Header = item.DisplayName;
                mi.Tag = item;

                switch (item.IldType)
                {
                    case IldType.String:
                        MenuItem msi1 = new MenuItem();
                        msi1.Header = "Contains...";
                        mi.Items.Add(msi1);

                        MenuItem msi2 = new MenuItem();
                        msi2.Header = "Does Not Contain...";
                        mi.Items.Add(msi2);
                        break;
                    case IldType.Integer:
                        ListNumberMenuItems(item, mi);
                        break;
                    case IldType.Double:
                        ListNumberMenuItems(item, mi);
                        break;
                    case IldType.Boolean:
                        MenuItem mbi1 = new MenuItem();
                        mbi1.Header = "True (Checked)";
                        mi.Items.Add(mbi1);

                        MenuItem mbi2 = new MenuItem();
                        mbi2.Header = "False (Unchecked)";
                        mi.Items.Add(mbi2);
                        break;
                    default:
                        break;
                }

                cm.Items.Add(mi);
            }

            cm.Items.Add(new Separator());

            MenuItem mcli = new MenuItem();
            mcli.Header = "Clear All Filters";
            cm.Items.Add(mcli);

            btnFilter.Menu = cm;

            void ListNumberMenuItems(IldPropertyInfo item, MenuItem mi)
            {
                if (item.MinValue != null && item.MaxValue != null)
                {
                    int min = item.MinValue.Value;
                    int max = item.MaxValue.Value;

                    if (max - min < 10)
                    {
                        for (int i = min; i <= max; i++)
                        {
                            MenuItem mni = new MenuItem();
                            mni.Header = i.ToString();
                            mi.Items.Add(mni);
                        }
                    }
                    else
                    {
                        MenuItem mni1 = new MenuItem();
                        mni1.Header = "Equals...";
                        mi.Items.Add(mni1);
                    }

                    mi.Items.Add(new Separator());

                    MenuItem mni2 = new MenuItem();
                    mni2.Header = "Is Between...";
                    mi.Items.Add(mni2);

                    MenuItem mni3 = new MenuItem();
                    mni3.Header = "Is Not Between...";
                    mi.Items.Add(mni3);
                }
            }
        }

        private void btnAdd_Click(object sender, RoutedEventArgs e)
        {
            if (DisplayElementType == null) return;

            var newItem = Activator.CreateInstance(DisplayElementType);

            SelectableListItem sli = (SelectableListItem)newItem!;

            sli.RequestDelete += sli_RequestDelete;
            sli.RequestMoveDown += sli_RequestMoveDown;
            sli.RequestMoveUp += sli_RequestMoveUp;
            sli.ContentChanged += sli_ContentChanged;

            selPanel.AddItem(sli);

            ContentChanged?.Invoke(this, EventArgs.Empty);
        }

        private void sli_ContentChanged(object? sender, EventArgs e)
        {
            ContentChanged?.Invoke(this, EventArgs.Empty);
        }

        private void sli_RequestMoveUp(object? sender, EventArgs e)
        {
            //throw new NotImplementedException();
        }

        private void sli_RequestMoveDown(object? sender, EventArgs e)
        {
            //throw new NotImplementedException();
        }

        private void sli_RequestDelete(object? sender, EventArgs e)
        {
            if (sender is SelectableListItem sli)
            {
                selPanel.RemoveItem(sli);

                ContentChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private void btnDeselect_Click(object sender, RoutedEventArgs e)
        {
            selPanel.DeselectAll();
        }

        private void btnShowHide_Click(object sender, RoutedEventArgs e)
        {
            if (rowButtons.MinHeight > 0)
            {
                // hide list
                rowButtons.MinHeight = 0;
                rowButtons.Height = new GridLength(0);

                rowPanel.MinHeight = 0;
                rowPanel.Height = new GridLength(0);

                imgShowHide.ImageName = "DownArrow";
                txtShowHide.Text = "Show List";
            }
            else
            {
                // show list
                rowButtons.MinHeight = 32;
                rowButtons.Height = new GridLength(1, GridUnitType.Auto);

                rowPanel.MinHeight = 20;
                rowPanel.Height = new GridLength(1, GridUnitType.Star);

                imgShowHide.ImageName = "UpArrow";
                txtShowHide.Text = "Hide List";
            }
        }
    }

    public enum IldType
    {
        String = 0,
        Integer = 1,
        Double = 2,
        Boolean = 3
    }
}

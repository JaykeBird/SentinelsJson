﻿using System;
using System.Collections.Generic;
using System.Linq;
using static SolidShineUi.Keyboard.KeyboardConstants;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;

namespace SolidShineUi.Keyboard
{
    public class KeyboardShortcutEventArgs
    {
        public KeyboardShortcut KeyboardShortcut { get; private set; }

        public KeyboardShortcutEventArgs(KeyboardShortcut shortcut)
        {
            KeyboardShortcut = shortcut;
        }
    }

    public class KeyRegistry
    {
        public delegate void KeyboardShortcutEventHandler(object sender, KeyboardShortcutEventArgs e);

        public event KeyboardShortcutEventHandler? ShortcutRegistered;
        public event KeyboardShortcutEventHandler? ShortcutUnregistered;

        public List<KeyboardShortcut> Ksr_All = new List<KeyboardShortcut>();

        public Dictionary<Key, KeyboardShortcut> Ksr_None = new Dictionary<Key, KeyboardShortcut>();
        public Dictionary<Key, KeyboardShortcut> Ksr_Ctrl = new Dictionary<Key, KeyboardShortcut>();
        public Dictionary<Key, KeyboardShortcut> Ksr_Alt = new Dictionary<Key, KeyboardShortcut>();
        public Dictionary<Key, KeyboardShortcut> Ksr_Shift = new Dictionary<Key, KeyboardShortcut>();
        public Dictionary<Key, KeyboardShortcut> Ksr_AltShift = new Dictionary<Key, KeyboardShortcut>();
        public Dictionary<Key, KeyboardShortcut> Ksr_CtrlAlt = new Dictionary<Key, KeyboardShortcut>();
        public Dictionary<Key, KeyboardShortcut> Ksr_CtrlShift = new Dictionary<Key, KeyboardShortcut>();
        public Dictionary<Key, KeyboardShortcut> Ksr_CtrlAltShift = new Dictionary<Key, KeyboardShortcut>();

        public void RegisterKeyShortcut(KeyboardShortcut kc)
        {
            Ksr_All.Add(kc);

            switch (kc.Combination)
            {
                case KeyboardCombination.None:
                    if (SafeKeys.Contains(kc.Key))
                    {
                        Ksr_None.Add(kc.Key, kc);
                    }
                    break;
                case KeyboardCombination.Ctrl:
                    Ksr_Ctrl.Add(kc.Key, kc);
                    break;
                case KeyboardCombination.Alt:
                    if (SafeKeys.Contains(kc.Key))
                    {
                        Ksr_Alt.Add(kc.Key, kc);
                    }
                    break;
                case KeyboardCombination.Shift:
                    if (SafeKeys.Contains(kc.Key))
                    {
                        Ksr_Shift.Add(kc.Key, kc);
                    }
                    break;
                case KeyboardCombination.AltShift:
                    Ksr_AltShift.Add(kc.Key, kc);
                    break;
                case KeyboardCombination.CtrlAlt:
                    Ksr_CtrlAlt.Add(kc.Key, kc);
                    break;
                case KeyboardCombination.CtrlShift:
                    Ksr_CtrlShift.Add(kc.Key, kc);
                    break;
                case KeyboardCombination.CtrlAltShift:
                    Ksr_CtrlAltShift.Add(kc.Key, kc);
                    break;
            }

            ShortcutRegistered?.Invoke(this, new KeyboardShortcutEventArgs(kc));
        }

        public void RegisterKeyShortcut(KeyboardCombination combination, Key key, RoutedEventHandler method, string methodId, MenuItem? menuItem)
        {
            KeyboardShortcut kc = new KeyboardShortcut(combination, key, method, methodId, menuItem);
            RegisterKeyShortcut(kc);
        }

        public void RegisterKeyShortcut(KeyboardCombination combination, Key key, (string methodId, RoutedEventHandler method, MenuItem? menuItem) methodRegistryItem)
        {
            KeyboardShortcut kc = new KeyboardShortcut(combination, key, methodRegistryItem.method, methodRegistryItem.methodId, methodRegistryItem.menuItem);
            RegisterKeyShortcut(kc);
        }

        public bool UnregisterKeyShortcut(KeyboardCombination combination, Key key)
        {
            bool success = true;

            KeyboardShortcut? ks = (from KeyboardShortcut kc in Ksr_All
                                   where kc.Key == key
                                   where kc.Combination == combination
                                   select kc).First() ?? null;

            if (ks != null)
            {
                Ksr_All.Remove(ks);
            }
            else
            {
                return false;
            }

            switch (combination)
            {
                case KeyboardCombination.None:
                    try
                    {
                        return Ksr_None.Remove(key);
                    }
                    catch (ArgumentNullException)
                    {
                        success = false;
                    }
                    break;
                case KeyboardCombination.Ctrl:
                    try
                    {
                        return Ksr_Ctrl.Remove(key);
                    }
                    catch (ArgumentNullException)
                    {
                        success = false;
                    }
                    break;
                case KeyboardCombination.Alt:
                    try
                    {
                        return Ksr_Alt.Remove(key);
                    }
                    catch (ArgumentNullException)
                    {
                        success = false;
                    }
                    break;
                case KeyboardCombination.Shift:
                    try
                    {
                        return Ksr_Shift.Remove(key);
                    }
                    catch (ArgumentNullException)
                    {
                        success = false;
                    }
                    break;
                case KeyboardCombination.AltShift:
                    try
                    {
                        return Ksr_AltShift.Remove(key);
                    }
                    catch (ArgumentNullException)
                    {
                        success = false;
                    }
                    break;
                case KeyboardCombination.CtrlAlt:
                    try
                    {
                        return Ksr_CtrlAlt.Remove(key);
                    }
                    catch (ArgumentNullException)
                    {
                        success = false;
                    }
                    break;
                case KeyboardCombination.CtrlShift:
                    try
                    {
                        return Ksr_CtrlShift.Remove(key);
                    }
                    catch (ArgumentNullException)
                    {
                        success = false;
                    }
                    break;
                case KeyboardCombination.CtrlAltShift:
                    try
                    {
                        return Ksr_CtrlAltShift.Remove(key);
                    }
                    catch (ArgumentNullException)
                    {
                        success = false;
                    }
                    break;
            }

            ShortcutUnregistered?.Invoke(this, new KeyboardShortcutEventArgs(new KeyboardShortcut(combination, key, null, "", null)));

            return success;
        }

        /// <summary>
        /// Set if menu items should display the keyboard shortcut combinations directly in the user interface. 
        /// </summary>
        /// <param name="display">True to display keyboard shortcut combinations, false to not have them displayed.</param>
        public void ApplyDisplaySettings(bool display)
        {
            foreach (KeyboardShortcut item in Ksr_All)
            {
                item.ApplyKeyDisplaySettings(display);
            }
        }

        /// <summary>
        /// Get a list of keyboard shortcuts registered to a certain method.
        /// </summary>
        /// <param name="methodId">The name of the method. If you used the RoutedMethodRegistry to fill from a menu, the name will be the name of the MenuItem itself.</param>
        /// <returns></returns>
        public IEnumerable<KeyboardShortcut> GetShortcutsForMethod(string methodId)
        {
            return from KeyboardShortcut kc in Ksr_All where kc.MethodId == methodId select kc;
        }

        /// <summary>
        /// Get a list of keyboard shortcuts registered with a certain RoutedEventHandler.
        /// </summary>
        public IEnumerable<KeyboardShortcut> GetShortcutsForMethod(RoutedEventHandler method)
        {
            return from KeyboardShortcut kc in Ksr_All where kc.Method == method select kc;
        }

        /// <summary>
        /// Get the RoutedEventHandler associated with a certain keyboard shortcut.
        /// </summary>
        /// <param name="key">The key for this shortcut.</param>
        /// <param name="shift">Set if the Shift key is part of this shortcut.</param>
        /// <param name="alt">Set if the Alt key is part of this shortcut.</param>
        /// <param name="ctrl">Set if the Ctrl key is part of this shortcut.</param>
        /// <returns></returns>
        public (RoutedEventHandler?, string) GetMethodForKey(Key key, bool shift, bool alt, bool ctrl)
        {
            if (ctrl)
            {
                if (shift)
                {
                    if (alt)
                    {
                        // Ctrl + Shift + Alt + whatever
                        if (Ksr_CtrlAltShift.ContainsKey(key))
                        {
                            return ((Ksr_CtrlAltShift[key]).Method, "* Ctrl + Shift + Alt + " + key);
                        }
                        else
                        {
                            return (null, "Ctrl + Shift + Alt + " + key);
                        }
                    }

                    // Ctrl + Shift + whatever
                    if (Ksr_CtrlShift.ContainsKey(key))
                    {
                        return ((Ksr_CtrlShift[key]).Method, "* Ctrl + Shift + " + key);
                    }
                    else
                    {
                        return (null, "Ctrl + Shift + " + key);
                    }
                }

                if (alt)
                {
                    // Ctrl + Alt + whatever
                    if (Ksr_CtrlAlt.ContainsKey(key))
                    {
                        return ((Ksr_CtrlAlt[key]).Method, "* Ctrl + Alt + " + key);
                    }
                    else
                    {
                        return (null, "Ctrl + Alt + " + key);
                    }
                }

                // Ctrl + whatever
                if (Ksr_Ctrl.ContainsKey(key))
                {
                    return ((Ksr_Ctrl[key]).Method, "* Ctrl + " + key);
                }
                else
                {
                    return (null, "Ctrl + " + key);
                }
            }

            if (alt)
            {
                if (shift)
                {
                    // Alt + Shift + whatever
                    if (Ksr_AltShift.ContainsKey(key))
                    {
                        return ((Ksr_AltShift[key]).Method, "* Shift + Alt + " + key);
                    }
                    else
                    {
                        return (null, "Shift + Alt + " + key);
                    }
                }

                // Alt + whatever
                // (Note: only some keys are allowed for Alt + key shortcuts)
                if (Ksr_Alt.ContainsKey(key))
                {
                    return ((Ksr_Alt[key]).Method, "* Alt + " + key);
                }
                else
                {
                    return (null, "Alt + " + key);
                }
            }

            if (shift)
            {
                // Shift + whatever
                // (Note: only some keys are allowed for Shift + key shortcuts)
                if (Ksr_Shift.ContainsKey(key))
                {
                    return ((Ksr_Shift[key]).Method, "* Shift + " + key);
                }
                else
                {
                    return (null, "Ctrl + Shift + " + key);
                }
            }

            // finally, just keys with no modifiers
            // (Note: only some keys are allowed for unmodified shortcuts)
            if (Ksr_None.ContainsKey(key))
            {
                return ((Ksr_None[key]).Method, "* " + key);
            }
            else
            {
                return (null, key.ToString());
            }
        }
    }

    public class SubKeyRegistry : KeyRegistry
    {
        private KeyRegistry masterRegistry = new KeyRegistry();

        public SubKeyRegistry(KeyRegistry master, Func<string, RoutedEventHandler> getMethodFunc, Func<string, MenuItem> getMenuFunc)
        {
            masterRegistry = master;
            GetMethodFunction = getMethodFunc;
            GetMenuFunction = getMenuFunc;

            master.ShortcutRegistered += Master_ShortcutRegistered;
            master.ShortcutUnregistered += Master_ShortcutUnregistered;
        }

        public void RegisterExistingShortcuts()
        {
            foreach (KeyboardShortcut kc in masterRegistry.Ksr_All)
            {
                try
                {
                    RegisterKeyShortcut(kc.Combination, kc.Key, GetMethodFunction(kc.MethodId), kc.MethodId, GetMenuFunction(kc.MethodId));
                }
                catch (ArgumentOutOfRangeException)
                {

                }
            }
        }

        public Func<string, RoutedEventHandler> GetMethodFunction { get; private set; }
        public Func<string, MenuItem> GetMenuFunction { get; private set; }

        private void Master_ShortcutRegistered(object sender, KeyboardShortcutEventArgs e)
        {
            KeyboardShortcut kc = e.KeyboardShortcut;

            try
            {
                RegisterKeyShortcut(kc.Combination, kc.Key, GetMethodFunction(kc.MethodId), kc.MethodId, GetMenuFunction(kc.MethodId));
            }
            catch (ArgumentOutOfRangeException)
            {

            }
        }

        private void Master_ShortcutUnregistered(object sender, KeyboardShortcutEventArgs e)
        {
            UnregisterKeyShortcut(e.KeyboardShortcut.Combination, e.KeyboardShortcut.Key);
        }

        public new void ApplyDisplaySettings(bool display)
        {
            foreach (KeyboardShortcut item in Ksr_All)
            {
                try
                {
                    MenuItem mi = GetMenuFunction(item.MethodId);

                    if (mi != null)
                    {
                        if (display)
                        {
                            mi.ToolTip = null;
                            mi.InputGestureText = item.KeyString;
                        }
                        else
                        {
                            mi.InputGestureText = "";
                            ToolTip tt = new ToolTip();
                            tt.Content = item.KeyString;
                            mi.ToolTip = tt;
                        }
                    }
                }
                catch (ArgumentOutOfRangeException)
                {

                }
            }
        }
    }
}

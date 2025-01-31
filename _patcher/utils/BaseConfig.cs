using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace _patcher.utils
{
    internal abstract class BaseConfig
    {
        internal void _saveConfig(StreamWriter writer)
        {
            PropertyInfo[] fields = GetFields();
            foreach (var propertyInfo in fields)
            {
                object value = propertyInfo.GetValue(this);
                if (value != null)
                {
                    // if the property a string, ensure its not empty before writing
                    string text = value as string;
                    if (text == null || text.Length != 0)
                    {
                        writer.WriteLine($"{propertyInfo.Name}={value}");
                    }
                }
            }
        }

        internal void _loadConfig(StreamReader reader)
        {
            var fields = GetFields().ToDictionary(p => p.Name, p => p);
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                // split each line to a property name and value
                // really configmanager in a nutshell
                string[] parts = line.Split(new char[] { '=' }, 2);
                if (parts.Length == 2 && fields.TryGetValue(parts[0], out var propertyInfo))
                {
                    object value = Convert.ChangeType(parts[1], propertyInfo.PropertyType);
                    propertyInfo.SetValue(this, value);
                }
            }
        }

        protected PropertyInfo[] GetFields() =>
            GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
    }
}
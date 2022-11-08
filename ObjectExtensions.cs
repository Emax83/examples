using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace MyProject.Extensions
{
    public static class ObjectExtensions
    {
        public static Dictionary<string,string> ToKeyValuePairs(this object theObject)
        {
            Dictionary<string, string> keyValuePairs = new Dictionary<string, string>();
            foreach(PropertyInfo prop in theObject.GetType().GetProperties())
            {
                object value = prop.GetValue(theObject);
                if (value == DBNull.Value || value == null)
                {
                    value = "";
                }
                keyValuePairs.Add(prop.Name, value.ToString());
            }
            return keyValuePairs;
        }

        public static string ToCSV<T>(this IEnumerable<T> items, char separator = ';') where T : class
        {
            StringBuilder builder = new StringBuilder();
            PropertyInfo[] properties = typeof(T).GetProperties();
            foreach(var prop in properties)
            {
                builder.Append(string.Format("{0}{1}", prop.Name, separator));
            }
            builder.Append(Environment.NewLine);
            foreach (T item in items)
            {
                foreach (var prop in properties)
                {
                    builder.Append(string.Format("{0}{1}", Convert.ToString(prop.GetValue(item)), separator));
                }

                builder.Append(Environment.NewLine);
            }
            return builder.ToString();
        }
    }
}

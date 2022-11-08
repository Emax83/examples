using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;

namespace Repository.Extensions
{
    public static class DataTableExtensions
    {

        /// <summary>
        /// Per usare questo metodo l'oggetto richiesto DEVE avere properties con NOMI IDENTICI alle colonne della tabella di origine
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="dataTable"></param>
        /// <returns></returns>
        public static IEnumerable<T> ToIEnumerableOf<T>(this DataTable dataTable)
        {
            List<T> list = new List<T>();

            if (dataTable == null || dataTable.Rows.Count == 0)
                return list;

            Type type = typeof(T);
            if (type.Name == "Object")
            {
                //attenzione metodo lento che occupa molta memoria!!!
                list = JsonConvert.DeserializeObject<List<T>>(JsonConvert.SerializeObject(dataTable, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore }));
            }
            else
            {
                string[] cols = dataTable.Columns.Cast<DataColumn>().Select(c => c.ColumnName.ToLower()).ToArray();
                PropertyInfo[] props = type.GetProperties().Where(p => p.CanWrite & cols.Contains(p.Name.ToLower())).ToArray();
                foreach (DataRow row in dataTable.Rows)
                {
                    T instance = Activator.CreateInstance<T>();
                    foreach (PropertyInfo prop in props)
                    {
                        try
                        {
                            if (row.IsNull(prop.Name))
                                continue;

                            var value = row[prop.Name];
                            if (value is byte[])
                                value = BitConverter.ToString((byte[])row[prop.Name]);

                            Type t = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
                            prop.SetValue(instance, Convert.ChangeType(value, t));
                        }
                        catch (Exception ex)
                        {
                            throw new Exception("Repository.DataTableExtensions.ToIEnumerableOf; Errore in fase di assegnazione proprietà '" + prop.Name + "': " + ex.Message, ex);
                        }
                    }
                    list.Add(instance);
                }
            }

            if (dataTable.Rows.Count != list.Count)
                throw new Exception("Repository.DataTableExtensions.ToIEnumerableOf; Non è stato possibile convertire alcuni oggetti.");

            return list;
        }


        public static string ToJSON(this DataTable dataTable)
        {
            return JsonConvert.SerializeObject(dataTable, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
        }

        public static string ToCSV(this DataTable dataTable, char separator = ';')
        {
            int r = 0; int c = 0;
            StringBuilder builder = new StringBuilder();
            for (c = 0; c < dataTable.Columns.Count; c++)
            {
                builder.Append(string.Format("{0}{1}", dataTable.Columns[c].ColumnName, separator));
            }
            builder.Append(Environment.NewLine);

            for (r = 0; r < dataTable.Rows.Count; r++)
            {
                for (c = 0; c < dataTable.Columns.Count; c++)
                {
                    builder.Append(string.Format("{0}{1}", dataTable.Rows[r][c], separator));
                }
                builder.Append(Environment.NewLine);
            }

            return builder.ToString();
        }

    }
}

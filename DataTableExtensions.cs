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

        
         public static IEnumerable<dynamic> ToIEnumerable(this DataTable dataTable)
        {
            List<dynamic> list = new List<dynamic>();

            if (dataTable == null || dataTable.Rows.Count == 0)
                return list;

            string[] cols = dataTable.Columns.Cast<DataColumn>().Select(c => c.ColumnName).ToArray();
            foreach (DataRow row in dataTable.Rows)
            {
                IDictionary<string,object> instance = new ExpandoObject();
                foreach (string colName in cols)
                {
                    try
                    {
                        //in questo caso non posso saltare, altrimenti non crea la property
                        var value = row[colName];

                        if (value == DBNull.Value)
                            value = null;

                        if (value is byte[])
                            value = BitConverter.ToString((byte[])row[colName]);

                        instance.Add(colName, value);
                        //instance[colName] = value;
                    }
                    catch (Exception ex)
                    {
                        throw new Exception("Repository.DataTableExtensions.ToIEnumerable; Errore in fase di creazione della proprietà '" + colName + "': " + ex.Message, ex);
                    }
                }
                list.Add(instance);
            }

            if (dataTable.Rows.Count != list.Count)
                throw new Exception("Repository.DataTableExtensions.ToIEnumerable; Non è stato possibile convertire alcuni oggetti.");

            return list;
        }
        
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
        
        public static byte[] ToExcel(this DataTable dataTable)
        {
            using (var memoryStream = new System.IO.MemoryStream())
                using (var writer = new System.IO.StreamWriter(memoryStream))
                {
                    writer.WriteLine("<html xmlns:v=\"urn:schemas-microsoft-com:vml\" xmlns:o=\"urn:schemas-microsoft-com:office:office\" xmlns:x=\"urn:schemas-microsoft-com:office:excel\" xmlns=\"http://www.w3.org/TR/REC-html40\">" +
                        "<meta http-equiv=Content-Type content=\"text/html;charset=windows-1252\">" +
                        "<meta name=ProgId content=Excel.Sheet>" +
                        "<meta name=Generator content=\"Microsoft Excel 11\">" +
                        "<style>table,td{font-family:Arial; font-size:10pt;color:black;}</style>" +
                        "<!--[if gte mso 9]><xml><x:ExcelWorkbook><x:ExcelWorksheets><x:ExcelWorksheet><x:Name>" + reportName + "</x:Name><x:WorksheetOptions><x:DisplayGridlines/></x:WorksheetOptions></x:ExcelWorksheet></x:ExcelWorksheets></x:ExcelWorkbook></xml><![endif]-->" +
                        "</head><body><table border='1' style='border-collapse: collapse;'>");

                    writer.WriteLine("<tr>");
                    foreach (ReportColumn column in columns)
                    {
                        writer.WriteLine(string.Format("<td style='font-weight:bold;background-color:#cccccc;'>{0}</td>", column.HeaderText ?? column.ColumnName));
                    }
                    writer.WriteLine("</tr>");

                    foreach (object item in list)
                    {
                        writer.WriteLine("<tr>");
                        foreach (ReportColumn column in columns)
                        {

                            System.Reflection.PropertyInfo property = item.GetType().GetProperty(column.ColumnName);
                            if (property != null && property.CanRead)
                            {
                                object value = property.GetValue(item);
                                string valueType = "String";


                                if (value == null || value is DBNull)
                                    value = "";

                                if (value is byte[])
                                    value = BitConverter.ToString((byte[])value);

                                //Type t = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
                                valueType = value.GetType().Name.Replace("System.", "").ToLower();
                                valueType = column.DataType.Replace("System.", "").ToLower();
                                switch (valueType)
                                {
                                    case "date":
                                    case "datetime":
                                        //mso-number-format:"dd\/mm\\ hh\:mm"  --> "dd/MM HH:mm"
                                        if (string.IsNullOrEmpty(column.ExcelFormat))
                                            column.ExcelFormat = "Short Date";
                                        writer.WriteLine(string.Format("<td align=right style='mso-number-format:\"{1}\";'>{0}</td>", value, column.ExcelFormat));
                                        break;

                                    case "decimal":
                                    case "int16":
                                    case "int32":
                                    case "int64":
                                    case "int":
                                    case "integer":
                                        //Fixed = formato numero con decimali
                                        //Standard = formato numero con decimali e separatore di migliaia
                                        if (string.IsNullOrEmpty(column.ExcelFormat))
                                            column.ExcelFormat = "Standard";
                                        writer.WriteLine(string.Format("<td align=right style='mso-number-format:{1};'>{0}</td>", value, column.ExcelFormat));
                                        break;

                                    default://string
                                        string bgColor = "#ffffff";
                                        if (column.ColumnName == "sDescrizioneSLA")
                                        {
                                            switch (value)
                                            {
                                                case "Attivo":
                                                    bgColor = "#2196F3";
                                                    break;

                                                case "Scade Domani":
                                                    bgColor = "#4CAF50";
                                                    break;

                                                case "Scade Oggi":
                                                    bgColor = "#ffeb3b";
                                                    break;

                                                case "Scaduto":
                                                    bgColor = "#f44336";
                                                    break;

                                                case "N/A":
                                                    bgColor = "#dddddd";
                                                    break;
                                            }

                                        }
                                        if (string.IsNullOrEmpty(column.ExcelFormat))
                                            column.ExcelFormat = "\\@";
                                        writer.WriteLine(string.Format("<td style='mso-number-format:\"{1}\"; background-color:{2};'>{0}</td>", Convert.ToString(value).Trim(), column.ExcelFormat, bgColor));
                                        break;
                                }

                            }
                        }
                        writer.WriteLine("</tr>");
                    }

                    writer.WriteLine("</table></body></html>");

                    writer.Flush();
                    times.Add("Creazione Excel", DateTime.UtcNow - start);
                    return memoryStream.ToArray();
                }
        }

    }
}

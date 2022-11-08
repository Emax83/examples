using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace RischioPaperless.Extensions
{
     public static class StringExtensions
    {
        public static string Left(this string value, int length)
        {
            if (string.IsNullOrEmpty(value))
                return value;

            if (value.Length <= length)
                return value;

            return value.Substring(0, length);
        }

        public static string Right(this string value,int length)
        {
            if (string.IsNullOrEmpty(value))
                return value;

            if (value.Length <= length)
                return value;

            return value.Substring(value.Length - length);
        }

        public static bool IsDateTime(string text)
        {
            DateTime dateTime;
            bool isDateTime = false;

            if (string.IsNullOrEmpty(text))
                return false;
            
            isDateTime = DateTime.TryParse(text, out dateTime);

            return isDateTime;
        }

        public static string Base64Encode(this string plainText)
        {
            var plainTextBytes = System.Text.Encoding.Default.GetBytes(plainText);
            return Convert.ToBase64String(plainTextBytes);
        }

        public static string Base64Decode(this string base64EncodedData)
        {
            var base64EncodedBytes = Convert.FromBase64String(base64EncodedData);
            return System.Text.Encoding.Default.GetString(base64EncodedBytes);
        }

        public static string DecodeHtml(this string value)
        {
            string[] sArr = new string[] { "\r\n", "<br />", "<br/>" };
            foreach (var s in sArr)
                value = HttpUtility.HtmlDecode(value.Replace(s, "\n"));

            if (value.Length > 2 && value.Substring(value.Length - 2, 2) == "\n")
                value = value.Substring(0, value.Length - 2);

            return value;
        }

        public static string EncodeHtml(this string value)
        {
            string[] sArr = new string[] { "\n", "\r\n" };
            foreach (var s in sArr)
                value = HttpUtility.HtmlDecode(value.Replace(s, "<br />"));
            return value;
        }
    }
}

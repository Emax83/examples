using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net;
using System.IO;
using System.Drawing;

namespace MyProjectName.Controllers
{

    public class BaseController : Controller
    {

        public BaseController()
        {

            ViewBag.ApplicationName = Settings.Current.ApplicationName;
            ViewData["ApplicationName"] = Settings.Current.ApplicationName;

            if (?????)
            {
                System.Web.Helpers.AntiForgeryConfig.UniqueClaimTypeIdentifier = System.Security.Claims.ClaimTypes.NameIdentifier;
            }
            else
            {
                System.Web.Helpers.AntiForgeryConfig.UniqueClaimTypeIdentifier = System.Security.Claims.ClaimTypes.Name;
            }
        }

        protected override void OnException(ExceptionContext filterContext)
        {
            try
            {

                if (filterContext.Exception is HttpAntiForgeryException)
                    return;

                EmailService.SendMailError(filterContext.Exception, filterContext.HttpContext.ApplicationInstance.Context);
                LogError("Controller OnException", filterContext.Exception);
            }
            catch (Exception ex)
            {
                LogError("BaseController OnException;", ex.Message);
            }
        }

       

        internal string GetCurrentIpAddess()
        {
            try
            {
                string ipAddress = HttpContext.Request.UserHostAddress;
                string ipForwarded = HttpContext.Request.ServerVariables["X_FORWARDED_FOR"];

                if (string.IsNullOrEmpty(ipForwarded) == false)
                    ipAddress = ipForwarded;

                return ipAddress;
            }
            catch (Exception ex)
            {
                return "GetCurrentIpAddess; Error: " + ex.Message;
            }

        }

        internal string GetModelStateErrors()
        {
            if (ModelState.IsValid)
            {
                return "";
            }
            else
            {
                var errors = ModelState
                    .Where(x => x.Value.Errors.Any())
                    .Select(x => x.Value.Errors
                    .Select(e => string.Format("{0}: {1};", x.Key, e.ErrorMessage))
                    .ToList())
                    .ToList();
                string error = string.Join("\n", errors);
                return error;
            }
        }


        internal void LogSuccess(string title, string message)
        {
            LogMessage(title, message);
            ViewData["MessageCssClass"] = "alert alert-success";
            Logger.Write(LogTypeMessage.Information, title + " - " + message);
        }

        internal void LogWarning(string title, string message)
        {
            LogMessage(title, message);
            ViewData["MessageCssClass"] = "alert alert-warning";
            Logger.Write(LogTypeMessage.Warning, title + " - " + message);
        }

        internal void LogError(string title, string message, bool alertUser = true, bool sendMail = false)
        {
            if (alertUser)
            {
                LogMessage(title, message);
                ViewData["MessageCssClass"] = "alert alert-danger";
            }

            Logger.Write(LogTypeMessage.Error, title + " - " + message);

            if (sendMail)
                EmailService.SendMailError(new Exception(title + message));

        }

        internal void LogError(string title, Exception exception, bool alertUser = true, bool sendMail = false)
        {
            string message = exception.Message;

            if (exception.InnerException != null)
                message += Environment.NewLine + exception.InnerException.Message;

            if (alertUser)
            {
                LogMessage(title, message);
                ViewData["MessageCssClass"] = "alert alert-danger";
            }
            
            Logger.Write(LogTypeMessage.Error, title + " - " + exception.ToString());

            if (sendMail)
                EmailService.SendMailError(exception);

        }

        private void LogMessage(string title, string message)
        {
            ViewData["MessageTitle"] = title;
            ViewData["MessageText"] = message;
        }

        internal bool IsNumeric(string value)
        {
            string pattern = @"^(\d+)$";
            return System.Text.RegularExpressions.Regex.IsMatch(value, pattern);
        }

        internal bool IsValidEmail(string value)
        {
            if (string.IsNullOrEmpty(value))
                return false;
            try
            {
                var mail = new System.Net.Mail.MailAddress(value);
                return true;
            }
            catch
            {
                return false;
            }
        }

        internal bool IsValidFile(HttpPostedFileBase file, string pattern = "")
        {
            try
            {
                if (file == null)
                    return false;

                if (file.ContentLength == 0)
                    return false;

                if(file.ContentLength >  Settings.Current.MaxFileSizeUploadBytes)
                    return false;

                if (string.IsNullOrEmpty(pattern))
                    pattern = Settings.Current.AllowedUploadExtensions;

                if (string.IsNullOrEmpty(pattern) == false)
                {
                    string ext = Path.GetExtension(RemoveDoubleExtension(file.FileName)).ToLower();

                    //verifico che il mime type corrisponda a quello passato
                    string mimeType = MimeMapping.GetMimeMapping(RemoveDoubleExtension(file.FileName));
                    if (mimeType == file.ContentType && pattern.ToLower().Contains(ext.ToLower()))
                    {
                        //verifico che il file non sia in realtà un file contenente caratteri codice e rinominato in immagine o altro.
                        //questo metodo mantiene valido il file.InputStream. Con uno streamreader verrebbe chiuso e il file tornerebbe a 0 bytes.
                        string inputContentString = "";
                        byte[] contentBytes = new byte[file.InputStream.Length];
                        file.InputStream.Seek(0, SeekOrigin.Begin);
                        file.InputStream.Read(contentBytes, 0, contentBytes.Length);
                        inputContentString = System.Text.Encoding.ASCII.GetString(contentBytes);
                        if (System.Text.RegularExpressions.Regex.IsMatch(inputContentString, @"(<body)|(<script)|(<html)|(<iframe)|(src=)|(href=)|(public)|(void)|(function)|(return)|(<?xml)|(DOCTYPE)|(svg)",System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                        {
                            contentBytes = null;
                            inputContentString = null;
                            return false;
                        }
                        
                        //verifico in base ai magic bytes che posso caricare il file.
                        if (CheckMagicBytes(contentBytes, pattern) == false)
                        {
                            contentBytes = null;
                            inputContentString = null;
                            return false;
                        }
                        
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }

                return true;
            }
            catch(Exception ex)
            {
                Logger.Write(LogTypeMessage.Error, "BaseController.IsValidFile", ex);
                return false;
            }
        }

        

        /// <summary>
        /// Ritorna un filename senza la doppia estensione se presente.
        /// </summary>
        /// <seealso cref="https://www.codeproject.com/Questions/886361/Prevent-double-file-Extension-file-upload-in-cshar"/>
        /// <param name="fileName"></param>
        /// <returns></returns>
        internal string RemoveDoubleExtension(string fileName)
        {
            string result = fileName;
            try
            {
                /*
                 * file.nome.pdf
                 * file nome.anno mese.docx                
                 * eccetera...
                 */
                if (fileName.Split('.').Length > 2)
                {
                    result = Path.GetFileNameWithoutExtension(fileName);
                }
            }
            catch(Exception ex)
            {
                Logger.Write(LogTypeMessage.Error, "BaseController.RemoveDoubleExtension('" + fileName + "')", ex);
            }
            return result;
        }

        internal string GetFileSize(long length)
        {
            string fileSize = $"{length} bytes";
            if (length > (1024 * 1024))
                fileSize = $"{(length / (1024 * 1024)).ToString("N0")} MB";

            else if (length > 1024)
                fileSize = $"{(length / 1024).ToString("N0")} KB";

            return fileSize;
        }

        /// <summary>
        /// Ridimensiona una immagine mantenendo il ratio. Serve per eliminare codice malevolo inserito in immagini jpg/png
        /// </summary>
        /// <param name="filePath"></param>
        internal void ResizeImage(string filePath, int width = 1024)
        {
            try
            {
                int height = 0;
                decimal ratio = 0;
                Image source = Image.FromFile(filePath);
                ratio = Convert.ToDecimal(source.Width) / Convert.ToDecimal(source.Height);
                height = Convert.ToInt32(width / ratio);
                Bitmap dest = new Bitmap(source,new Size(width,height));//, width, height, source.PixelFormat);
                source.Dispose();
                dest.Save(filePath, System.Drawing.Imaging.ImageFormat.Jpeg);
                dest.Dispose();
            }
            catch (Exception ex)
            {
                LogError("BaseController.ResizeImage", ex);
                if (System.IO.File.Exists(filePath))
                    System.IO.File.Delete(filePath);
                throw new Exception("Si è verificato un problema con questa immagine. Prova con un altra.");
            }
        }

        /// <summary>
        /// Verifica la signature del file per verificare che sia veramente il file che dice di essere e che sia consentito.
        /// </summary>
        /// <param name="fileContent"></param>
        /// <param name="extPattern"></param>
        /// <returns></returns>
        internal bool CheckMagicBytes(byte[] fileContent, string extPattern)
        {
            //dato che non posso controllare tutte le possibili estensioni, controllo solo quelle consentite, il resto non è consentito.
            //https://en.wikipedia.org/wiki/List_of_file_signatures
            //https://medium.com/@d.harish008/what-is-a-magic-byte-and-how-to-exploit-1e286da1c198
            bool isValid = false;
            try
            {
                byte[] magicBytes = new byte[16]; // Read 16 bytes into an array    
                Buffer.BlockCopy(fileContent, 0, magicBytes, 0, 16);
                string hexSignature = BitConverter.ToString(magicBytes);
                string ext = ""; //.pdf|.docx|.doc|.xlsx|.xls|.jpg|.jpeg|.png|.zip|.txt|.eml|.msg
                switch (hexSignature)
                {
                    case "aa":
                        ext = ".pdf";
                        break;
                    case "bb":
                        ext = ".docx";
                        break;
                    case "cc":
                        ext = ".doc";
                        break;
                    case "dd":
                        ext = ".xlsx";
                        break;
                    case "ee":
                        ext = ".xls";
                        break;
                    case "ff":
                        ext = ".jpg";
                        break;
                    case "gg":
                        ext = ".png";
                        break;
                    case "hh":
                        ext = ".zip";
                        break;
                    case "iii":
                        ext = ".txt";
                        break;
                    case "JJ":
                        ext = ".eml";
                        break;
                    case "kk":
                        ext = ".msg";
                        break;
                    default:
                        return false;
                }
                if (string.IsNullOrEmpty(extPattern) == false)
                {
                    isValid = extPattern.ToLower().Contains(ext);
                }
                else
                {
                    isValid = true;
                }
            }
            catch (Exception ex)
            {
                Logger.Write(LogTypeMessage.Error, "BaseController.IsValidFile", ex);
                isValid = false;
            }
            return isValid;
        }
    }
}

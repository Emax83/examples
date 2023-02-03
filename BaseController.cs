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

                if (file.ContentLength > Settings.Current.MaxFileSizeUploadBytes)
                    return false;

                if (string.IsNullOrEmpty(pattern))
                    pattern = Settings.Current.AllowedUploadExtensions;

                string fileExtension = Path.GetExtension(file.FileName).ToLower();
                if (string.IsNullOrEmpty(fileExtension))
                    return false; //file senza estensione

                if (string.IsNullOrEmpty(pattern) == false)
                {
                    //verifico che il mime type corrisponda a quello passato
                    string mimeType = MimeMapping.GetMimeMapping(file.FileName);
                    if (mimeType != file.ContentType || pattern.ToLower().Contains(fileExtension.ToLower()) == false)
                    {
                        return false;
                    }
                }

                if (Settings.Current.CheckContentString)
                {
                    //verifico che il file non sia in realtà un file contenente caratteri codice e rinominato in immagine o altro.
                    //questo metodo mantiene valido il file.InputStream. Con uno streamreader verrebbe chiuso e il file tornerebbe a 0 bytes.
                    byte[] contentBytes = new byte[file.InputStream.Length];
                    file.InputStream.Seek(0, SeekOrigin.Begin);
                    file.InputStream.Read(contentBytes, 0, contentBytes.Length);
                    if (CheckContentString(contentBytes, file.FileName) == false)
                    {
                        contentBytes = null;
                        return false;
                    }
                    contentBytes = null;
                }

                if (Settings.Current.CheckMagicBytes)
                {
                    byte[] contentBytes = new byte[file.InputStream.Length];
                    file.InputStream.Seek(0, SeekOrigin.Begin);
                    file.InputStream.Read(contentBytes, 0, contentBytes.Length);
                    //verifico in base ai magic bytes che posso caricare il file.
                    if (CheckMagicBytes(contentBytes, file.FileName) == false)
                    {
                        contentBytes = null;
                        return false;
                    }
                    contentBytes = null;
                }

                return true;
            }
            catch (Exception ex)
            {
                General.Logger.Write(LogTypeMessage.Error, "BaseController.IsValidFile", ex);
                return false;
            }
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
        /// Verifica che il file non contenga del codice eseguibile o xss injection
        /// </summary>
        /// <param name="contentBytes"></param>
        /// <returns></returns>
        bool CheckContentString(byte[] contentBytes, string fileName)
        {
            string inputContentString = string.Empty;
            try
            {
                //attenzione: qui potrei bloccare file validi.
                //i file word,excel a volte contengono tag xml, si in versioni più recenti che vecchi.
                //oppure i file excel generati a partire da un html...
                //se proprio è necessario, stampano su PDF e poi lo allegano.
                inputContentString = System.Text.Encoding.ASCII.GetString(contentBytes);
                string patternCodice = @"(<body|<script|<html|<iframe|src=|href=|public |void |return |<\?doctype|<svg)";
                if (System.Text.RegularExpressions.Regex.IsMatch(inputContentString, patternCodice, System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                {
                    //inserisco in un log che qualcuno sta provando a caricare file malevolo contenente codice.
                    return false;
                }
                else
                {
                    //file pulito
                    return true;
                }
            }
            catch (Exception ex)
            {
                General.Logger.Write(LogTypeMessage.Error, "BaseController;CheckContentString;Errore: " + ex.ToString());
                return false;
            }
            finally
            {
                inputContentString = string.Empty;
            }
        }
        
        /// <summary>
        /// Verifica la signature del file per verificare che sia veramente il file che dice di essere e che sia consentito.
        /// </summary>
        /// <param name="fileContent"></param>
        /// <param name="extPattern"></param>
        /// <returns></returns>
        bool CheckMagicBytes(byte[] fileContent, string fileName)
        {
            //è un controllo complesso: diverse tipologie di files condividono gli stessi bytes oppure la stessa tipologia ha diversi bytes.
            //dato che non posso controllare tutte le possibili estensioni, controllo solo quelle consentite, il resto non è consentito.
            //https://en.wikipedia.org/wiki/List_of_file_signatures
            //https://medium.com/@d.harish008/what-is-a-magic-byte-and-how-to-exploit-1e286da1c198
            bool isValid = false;
            try
            {
                byte[] magicBytes = new byte[16]; // Read 16 bytes into an array    
                Buffer.BlockCopy(fileContent, 0, magicBytes, 0, 16);
                string hexSignature = BitConverter.ToString(magicBytes).Substring(0, 11);
                string validExtensions = "";
                switch (hexSignature)
                {
                    case "25-50-44-46":
                        validExtensions = ".pdf";
                        isValid = true;
                        break;
                    case "50-4B-03-04":
                        validExtensions = ".docx|.xlsx|.zip";
                        isValid = true;
                        break;
                    case "D0-CF-11-E0":
                        validExtensions = ".doc|.msg";
                        isValid = true;
                        break;
                    case "3C-68-74-6D":
                        validExtensions = ".xls";
                        isValid = true;
                        break;
                    case "FF-D8-FF-E0":
                        validExtensions = ".jpg";
                        isValid = true;
                        break;
                    case "89-50-4E-47":
                        validExtensions = ".png";
                        isValid = true;
                        break;
                    case "73-61-64-73":
                        validExtensions = ".txt";
                        isValid = true;
                        break;
                    case "58-2D-53-65":
                    case "52-65-63-65":
                        validExtensions = ".eml";
                        break;
                    default:
                        //inserisco in un log che qualcuno sta provando a caricare file malevolo
                        validExtensions = "";
                        isValid = false;
                        break;
                }
                //verifico se è una delle estensioni consentite, e se la possibile estensione corrisponde con quella del file.
                string fileExtension = Path.GetExtension(fileName);
                if (string.IsNullOrEmpty(validExtensions) == false && string.IsNullOrEmpty(fileExtension) == false)
                {
                    isValid = validExtensions.ToLower().Contains(fileExtension.ToLower());
                }
                else
                {
                    //default: nego il caricamento. 
                    isValid = false;
                }
            }
            catch (Exception ex)
            {
                General.Logger.Write(LogTypeMessage.Error, "BaseController.IsValidFile", ex);
                isValid = false;
            }
            return isValid;
        }
    }
}

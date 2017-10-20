using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage;

namespace ComEdRateLight
{
    internal class Logging
    {
        public static async Task WriteSystemLog(string strFormat, params string[] strParams)
        {
            await WriteLog("System", strFormat, strParams);
        }

        public static async Task WriteDebugLog(string strFormat, params string[] strParams)
        {
            await WriteLog("Debug", strFormat, strParams);
        }

        private static async Task WriteLog(string strLog, string strFormat, params string[] strParams)
        {
            DateTime dtNow;
            string strLogFile;
            string strLogData;
            StorageFolder storageFolder;
            StorageFile storageFile;

            try
            {
                dtNow = DateTime.Now;
                strLogFile = string.Format("{0}_{1}.Log", strLog, dtNow.ToString("yyyyMMdd"));
                strLogData = DateTime.Now.ToString("dd-MM-yyyy HH:mm:ss.ff ") + string.Format(strFormat, strParams);

                Debug.WriteLine(strLogData);

                // Apply Asynchronous Lock here
                {
                    storageFolder = Windows.Storage.ApplicationData.Current.LocalFolder;
                    storageFile = await storageFolder.CreateFileAsync(strLogFile, CreationCollisionOption.OpenIfExists);

                    using (StreamWriter writer = new StreamWriter(await storageFile.OpenStreamForWriteAsync()))
                    {
                        writer.BaseStream.Seek(0, SeekOrigin.End);
                        await writer.WriteLineAsync(strLogData);
                        writer.Flush();
                    }
                }
            }
            catch (Exception eException)
            {
                Debug.WriteLine(string.Format("Error: WriteSystemLog() {0}", eException.Message));
            }
        }
    }
}

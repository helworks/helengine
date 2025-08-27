using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
#if WINFORMS
using System.Windows.Forms;
#endif

namespace Nucleus {
    public static class ApplicationUtil {
        /// <summary>
        /// TODO: bring my binary serializer back into Nucleus.Gaming
        /// Converts an object to JSON, then a Base64 string that can be passed to a program as start parameters
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static string GetObjectAsArgument(object data) {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(data)));
        }

        public static void PopulateObjectWithArgument(object target, string base64Str) {
            string base64 = Encoding.UTF8.GetString(Convert.FromBase64String(base64Str));
            JsonConvert.PopulateObject(base64, target);
        }

        public static bool OnlyOneInstance() {
            if (Process.GetProcessesByName(Process.GetCurrentProcess().ProcessName).Length > 1) {
#if WINFORMS
                MessageBox.Show("Nucleus Coop is already running.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Application.Exit();
#endif
                return true;
            }
            return false;
        }

        public static bool IsGameTasksApp() {
            string entryApp = Assembly.GetEntryAssembly().Location;
            return entryApp.ToLower().Contains("startgame");
        }

        public static string GetAppDataPath() {
#if ALPHA
            string entryApp = Assembly.GetEntryAssembly().Location;
            string local = Path.GetDirectoryName(entryApp);

            if (IsGameTasksApp()) {
                // game tasks application, move to correct folder
                return Path.Combine(local, "..", "data");
            } else {
                return Path.Combine(local, "data");
            }
#else
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, "Nucleus Coop");
#endif
        }

    }
}
